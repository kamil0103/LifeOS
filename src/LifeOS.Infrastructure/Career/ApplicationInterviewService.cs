using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LifeOS.Application.DTOs.Jobs;
using LifeOS.Application.Interfaces;
using LifeOS.Application.Services;
using LifeOS.Domain.Entities;
using LifeOS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
namespace LifeOS.Infrastructure.Career;
public class ApplicationInterviewService(AppDbContext db, TimeProvider clock) : IApplicationInterviewService
{
    private async Task<IDbContextTransaction?> LockAsync(Guid userId, CancellationToken ct)
    {
        if (!db.Database.IsRelational()) return null;
        var tx = await db.Database.BeginTransactionAsync(ct);
        try { var key = BitConverter.ToInt64(userId.ToByteArray(), 0); await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({key})", ct); return tx; }
        catch { await tx.DisposeAsync(); throw; }
    }
    public async Task<List<ApplicationInterviewDto>?> ListAsync(Guid userId, Guid applicationId, CancellationToken ct)
    {
        if (!await db.JobApplications.AnyAsync(a => a.Id == applicationId && a.UserId == userId, ct)) return null;
        var interviews = await db.ApplicationInterviews.AsNoTracking().Where(i => i.UserId == userId && i.ApplicationId == applicationId).OrderBy(i => i.StartTime).ToListAsync(ct);
        var events = await db.CalendarEvents.AsNoTracking().Where(e => e.UserId == userId && e.ApplicationId == applicationId).ToListAsync(ct);
        return interviews.Select(i => Map(i, events.FirstOrDefault(e => e.ActionKey == Key(i.Id))?.Id)).ToList();
    }
    private static string Key(Guid id) => "interview:" + id;
    private static string? Text(string? value, int max)
    {
        var text = ProfessionalRecordRules.Text(value); if (text?.Length > max) throw new InvalidOperationException($"Interview text must be at most {max} characters."); return text;
    }
    public async Task<ApplicationInterviewDto?> SaveAsync(Guid userId, Guid applicationId, Guid? id, SaveInterviewRequest r, CancellationToken ct)
    {
        var round = Text(r.Round, 150) ?? throw new InvalidOperationException("Enter an interview round.");
        if (!new[] { "phone", "video", "onsite", "other" }.Contains(r.Format) || !new[] { "scheduled", "completed", "cancelled" }.Contains(r.Status)) throw new InvalidOperationException("Choose a listed interview format and status.");
        if (r.StartTime == default || r.EndTime <= r.StartTime || r.EndTime - r.StartTime > TimeSpan.FromDays(1)) throw new InvalidOperationException("Choose an interview end after its start, within 24 hours.");
        var start = r.StartTime.ToUniversalTime(); var end = r.EndTime.ToUniversalTime();
        var interviewer = Text(r.Interviewer, 300); var location = Text(r.Location, 1000); var url = ProfessionalRecordRules.Url(r.MeetingUrl); var preparation = Text(r.Preparation, 10000); var outcome = Text(r.Outcome, 10000);
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { applicationId, round, r.Format, r.Status, start, end, interviewer, location, url, preparation, outcome }))));
        if (!id.HasValue && r.RequestId == Guid.Empty) throw new InvalidOperationException("An interview request identifier is required.");
        if (id.HasValue && !r.ExpectedUpdatedAt.HasValue) throw new InvalidOperationException("Reload the interview before editing it.");
        await using var tx = await LockAsync(userId, ct);
        var app = await db.JobApplications.Include(a => a.Job).FirstOrDefaultAsync(a => a.Id == applicationId && a.UserId == userId, ct); if (app == null) return null;
        ApplicationInterview? item;
        if (!id.HasValue) {
            item = await db.ApplicationInterviews.FirstOrDefaultAsync(i => i.UserId == userId && i.RequestId == r.RequestId, ct);
            if (item != null) {
                if (item.CreationFingerprint != fingerprint || item.ApplicationId != applicationId) throw new RecordConflictException("This save identifier was already used for different interview details. Reopen the interview list before creating another round.");
                var existingEvent = await db.CalendarEvents.FirstOrDefaultAsync(e => e.UserId == userId && e.ActionKey == Key(item.Id), ct);
                if (tx != null) await tx.CommitAsync(ct); return Map(item, existingEvent?.Id);
            }
            item = new() { UserId = userId, ApplicationId = applicationId, RequestId = r.RequestId, CreationFingerprint = fingerprint };
        } else {
            item = await db.ApplicationInterviews.FirstOrDefaultAsync(i => i.Id == id && i.UserId == userId && i.ApplicationId == applicationId, ct); if (item == null) return null;
            if (item.UpdatedAt.ToUnixTimeMilliseconds() != r.ExpectedUpdatedAt!.Value.ToUnixTimeMilliseconds()) throw new RecordConflictException("This interview changed since you opened it. Your edits are retained; reopen the latest interview before saving again.");
        }
        var key = Key(item.Id); var calendar = await db.CalendarEvents.FirstOrDefaultAsync(e => e.UserId == userId && e.ActionKey == key, ct);
        if (r.Status == "scheduled" && await db.CalendarEvents.AnyAsync(e => e.UserId == userId && e.ActionKey != key && e.StartTime < end && (e.EndTime ?? e.StartTime.AddMinutes(30)) > start, ct)) throw new RecordConflictException("This interview overlaps a local calendar event. Choose another time.");
        item.Round = round; item.Format = r.Format; item.Status = r.Status; item.StartTime = start; item.EndTime = end; item.Interviewer = interviewer; item.Location = location; item.MeetingUrl = url; item.Preparation = preparation; item.Outcome = outcome;
        item.UpdatedAt = DateTimeOffset.FromUnixTimeMilliseconds(Math.Max(clock.GetUtcNow().ToUnixTimeMilliseconds(), item.UpdatedAt.ToUnixTimeMilliseconds() + 1));
        var history = JsonSerializer.Deserialize<List<InterviewRevisionDto>>(item.HistoryJson) ?? []; history.Add(Revision(item)); item.HistoryJson = JsonSerializer.Serialize(history);
        if (item.Status == "cancelled") { if (calendar != null) db.CalendarEvents.Remove(calendar); calendar = null; }
        else {
            if (calendar == null) { calendar = new() { UserId = userId, ApplicationId = app.Id, JobId = app.JobId, ActionKey = key }; db.CalendarEvents.Add(calendar); }
            calendar.Title = ("Interview: " + app.Job.Company + " · " + round); if (calendar.Title.Length > 200) calendar.Title = calendar.Title[..200];
            calendar.StartTime = start; calendar.EndTime = end; calendar.EventType = "interview"; calendar.Location = location; calendar.Description = "Manage interview details and history in Application Tracker."; calendar.UpdatedAt = item.UpdatedAt;
        }
        if (!id.HasValue) db.ApplicationInterviews.Add(item);
        await db.SaveChangesAsync(ct); if (tx != null) await tx.CommitAsync(ct); return Map(item, calendar?.Id);
    }
    public async Task<bool> DeleteApplicationAsync(Guid userId, Guid applicationId, CancellationToken ct)
    {
        await using var tx = await LockAsync(userId, ct);
        var app = await db.JobApplications.FirstOrDefaultAsync(a => a.Id == applicationId && a.UserId == userId, ct); if (app == null) return false;
        db.CalendarEvents.RemoveRange(await db.CalendarEvents.Where(e => e.UserId == userId && e.ApplicationId == applicationId).ToListAsync(ct));
        db.ApplicationInterviews.RemoveRange(await db.ApplicationInterviews.Where(i => i.UserId == userId && i.ApplicationId == applicationId).ToListAsync(ct));
        db.JobApplications.Remove(app); await db.SaveChangesAsync(ct); if (tx != null) await tx.CommitAsync(ct); return true;
    }
    private static InterviewRevisionDto Revision(ApplicationInterview i) => new() { Round = i.Round, Format = i.Format, Status = i.Status, StartTime = i.StartTime, EndTime = i.EndTime, Interviewer = i.Interviewer, Location = i.Location, MeetingUrl = i.MeetingUrl, Preparation = i.Preparation, Outcome = i.Outcome, ChangedAt = i.UpdatedAt };
    private static ApplicationInterviewDto Map(ApplicationInterview i, Guid? eventId) => new() { Id = i.Id, ApplicationId = i.ApplicationId, CalendarEventId = eventId, Round = i.Round, Format = i.Format, Status = i.Status, StartTime = i.StartTime, EndTime = i.EndTime, Interviewer = i.Interviewer, Location = i.Location, MeetingUrl = i.MeetingUrl, Preparation = i.Preparation, Outcome = i.Outcome, ChangedAt = i.UpdatedAt, UpdatedAt = i.UpdatedAt, History = JsonSerializer.Deserialize<List<InterviewRevisionDto>>(i.HistoryJson) ?? [] };
}
