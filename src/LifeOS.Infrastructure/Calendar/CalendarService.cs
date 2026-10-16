using LifeOS.Application.DTOs.Calendar;
using LifeOS.Application.Interfaces;
using LifeOS.Domain.Entities;
using LifeOS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
namespace LifeOS.Infrastructure.Calendar;
public class CalendarService(AppDbContext db) : ICalendarService
{
    public async Task<List<CalendarEventDto>> GetAsync(Guid userId, DateTimeOffset? start, DateTimeOffset? end, CancellationToken ct)
    {
        if (start.HasValue && end.HasValue && (end <= start || end - start > TimeSpan.FromDays(370)))
            throw new InvalidOperationException("Choose a calendar range of at most one year.");
        var query = db.CalendarEvents.AsNoTracking().Where(e => e.UserId == userId);
        if (start.HasValue) query = query.Where(e => e.EndTime.HasValue ? e.EndTime.Value > start.Value : e.StartTime >= start.Value);
        if (end.HasValue) query = query.Where(e => e.StartTime < end.Value);
        return await query.OrderBy(e => e.StartTime).Select(e => new CalendarEventDto {
            Id=e.Id, ApplicationId=e.ApplicationId, Title=e.Title, Description=e.Description, StartTime=e.StartTime, EndTime=e.EndTime,
            IsAllDay=e.IsAllDay, Location=e.Location, EventType=e.EventType, Color=e.Color, IsRecurring=e.IsRecurring,
            HabitId=e.HabitId, HabitName=e.Habit != null ? e.Habit.Name : null, JobId=e.JobId, JobTitle=e.Job != null ? e.Job.Title : null
        }).ToListAsync(ct);
    }
    public async Task<CalendarEventDto> CreateAsync(Guid userId, CreateCalendarEventRequest request, CancellationToken ct)
    {
        Validate(request.Title, request.StartTime, request.EndTime);
        if (request.HabitId.HasValue && !await db.Habits.AnyAsync(h => h.Id == request.HabitId && h.UserId == userId,ct)) throw new InvalidOperationException("Habit not found.");
        if (request.JobId.HasValue && !await db.Jobs.AnyAsync(j => j.Id == request.JobId && j.UserId == userId,ct)) throw new InvalidOperationException("Job not found.");
        var e = new CalendarEvent { UserId=userId, ActionKey=request.ActionKey, Title=request.Title.Trim(), Description=request.Description, StartTime=request.StartTime.ToUniversalTime(), EndTime=request.EndTime?.ToUniversalTime(), IsAllDay=request.IsAllDay, Location=request.Location, EventType=request.EventType, Color=request.Color, HabitId=request.HabitId, JobId=request.JobId };
        db.CalendarEvents.Add(e); await db.SaveChangesAsync(ct); return Map(e);
    }
    public async Task<CalendarEventDto?> UpdateAsync(Guid userId, Guid id, UpdateCalendarEventRequest request, CancellationToken ct)
    {
        var e = await db.CalendarEvents.FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId, ct);
        if (e == null) return null;
        if (e.ActionKey?.StartsWith("interview:") == true) throw new InvalidOperationException("Edit or cancel this interview in Application Tracker to preserve its history.");
        Validate(request.Title, request.StartTime, request.EndTime);
        e.Title=request.Title.Trim(); e.Description=request.Description; e.StartTime=request.StartTime.ToUniversalTime(); e.EndTime=request.EndTime?.ToUniversalTime(); e.IsAllDay=request.IsAllDay; e.Location=request.Location; e.EventType=e.ApplicationId.HasValue?"followup":request.EventType; e.Color=request.Color; e.UpdatedAt=DateTimeOffset.UtcNow;
        if(e.ApplicationId.HasValue && e.EventType=="followup") {
            var application=await db.JobApplications.FirstOrDefaultAsync(a=>a.Id==e.ApplicationId && a.UserId==userId,ct);
            if(application!=null)application.FollowUpDate=e.StartTime.UtcDateTime;
        }
        await db.SaveChangesAsync(ct); return Map(e);
    }
    public async Task<bool> DeleteAsync(Guid userId, Guid id, CancellationToken ct)
    {
        var e=await db.CalendarEvents.FirstOrDefaultAsync(e=>e.Id==id && e.UserId==userId,ct);
        if(e==null)return false;
        if (e.ActionKey?.StartsWith("interview:") == true) throw new InvalidOperationException("Cancel this interview in Application Tracker to preserve its history.");
        if(e.ApplicationId.HasValue && e.EventType=="followup") { var application=await db.JobApplications.FirstOrDefaultAsync(a=>a.Id==e.ApplicationId && a.UserId==userId,ct);if(application!=null)application.FollowUpDate=null; }
        db.CalendarEvents.Remove(e); await db.SaveChangesAsync(ct); return true;
    }
    public static void Validate(string title, DateTimeOffset start, DateTimeOffset? end)
    {
        if(string.IsNullOrWhiteSpace(title) || title.Length>200) throw new InvalidOperationException("Enter a title of 1–200 characters.");
        if(start == default || end.HasValue && end.Value<=start) throw new InvalidOperationException("End time must be later than start time.");
    }
    private static CalendarEventDto Map(CalendarEvent e) => new() { Id=e.Id, ApplicationId=e.ApplicationId, Title=e.Title, Description=e.Description, StartTime=e.StartTime, EndTime=e.EndTime, IsAllDay=e.IsAllDay, Location=e.Location, EventType=e.EventType, Color=e.Color, HabitId=e.HabitId, JobId=e.JobId };
}
