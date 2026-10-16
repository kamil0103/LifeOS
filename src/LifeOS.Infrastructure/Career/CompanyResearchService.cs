using LifeOS.Application.DTOs.Jobs;
using LifeOS.Application.Interfaces;
using LifeOS.Application.Services;
using LifeOS.Domain.Entities;
using LifeOS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
namespace LifeOS.Infrastructure.Career;
public class CompanyResearchService(AppDbContext db) : ICompanyResearchService
{
    public async Task<List<CompanyNoteDto>> ListAsync(Guid userId, CancellationToken ct)
        => (await db.CompanyNotes.AsNoTracking().Where(n => n.UserId == userId).OrderByDescending(n => n.CreatedAt).ToListAsync(ct)).Select(Map).ToList();
    private static string? Text(string? value, int limit, string label)
    {
        var text = ProfessionalRecordRules.Text(value);
        if (text?.Length > limit) throw new InvalidOperationException($"{label} must be at most {limit} characters.");
        return text;
    }
    public async Task<CompanyNoteDto?> SaveAsync(Guid userId, Guid? id, CompanyResearchRequest r, CancellationToken ct)
    {
        var name = Text(r.CompanyName, 200, "Company name");
        if (name == null) throw new InvalidOperationException("Enter a company name.");
        if (r.Rating is < 1 or > 5) throw new InvalidOperationException("Choose a rating from 1 to 5.");
        var notes = Text(r.Notes, 20000, "Notes"); var website = ProfessionalRecordRules.Url(r.Website);
        var industry = Text(r.Industry, 200, "Industry"); var products = Text(r.Products, 10000, "Products");
        var technology = Text(r.TechnologyStack, 10000, "Technology stack"); var culture = Text(r.Culture, 10000, "Culture");
        var preparation = Text(r.InterviewPreparation, 10000, "Interview preparation");
        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
        if (tx != null) { var key = BitConverter.ToInt64(userId.ToByteArray(), 0) ^ 0x434F4D50414EL; await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({key})", ct); }
        var n = id.HasValue ? await db.CompanyNotes.FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId, ct) : new CompanyNote { UserId = userId };
        if (n == null) return null;
        if (id.HasValue && r.ExpectedUpdatedAt.HasValue && n.UpdatedAt.ToUnixTimeMilliseconds() != r.ExpectedUpdatedAt.Value.ToUnixTimeMilliseconds()) throw new RecordConflictException("Company research changed since you opened it. Your edits are retained; reopen the latest research before saving again.");
        n.CompanyName = name; n.Notes = notes; n.Rating = r.Rating;
        // Older clients omit the structured fields. Empty strings explicitly clear them.
        if (r.Website != null) n.Website = website;
        if (r.Industry != null) n.Industry = industry;
        if (r.Products != null) n.Products = products;
        if (r.TechnologyStack != null) n.TechnologyStack = technology;
        if (r.Culture != null) n.Culture = culture;
        if (r.InterviewPreparation != null) n.InterviewPreparation = preparation;
        if (!id.HasValue) db.CompanyNotes.Add(n);
        n.UpdatedAt = DateTimeOffset.FromUnixTimeMilliseconds(Math.Max(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), n.UpdatedAt.ToUnixTimeMilliseconds() + 1));
        await db.SaveChangesAsync(ct); if (tx != null) await tx.CommitAsync(ct);
        return Map(n);
    }
    public async Task<List<CompanyRelatedJobDto>?> RelatedJobsAsync(Guid userId, Guid id, CancellationToken ct)
    {
        var note = await db.CompanyNotes.AsNoTracking().FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId, ct);
        if (note == null) return null;
        var name = JobIdentity.Text(note.CompanyName);
        var jobs = (await db.Jobs.AsNoTracking().Where(j => j.UserId == userId).OrderByDescending(j => j.CreatedAt).ToListAsync(ct)).Where(j => JobIdentity.Text(j.Company) == name).ToList();
        var applications = await db.JobApplications.AsNoTracking().Where(a => a.UserId == userId).OrderByDescending(a => a.CreatedAt).ToListAsync(ct);
        return jobs.Select(j => { var app = applications.FirstOrDefault(a => a.JobId == j.Id); return new CompanyRelatedJobDto { Id = j.Id, Title = j.Title, Location = j.Location, SalaryRange = j.SalaryRange, Status = j.Status, ApplicationId = app?.Id, ApplicationStatus = app?.Status }; }).ToList();
    }
    public async Task<bool> DeleteAsync(Guid userId, Guid id, CancellationToken ct)
    {
        var note = await db.CompanyNotes.FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId, ct); if (note == null) return false;
        db.CompanyNotes.Remove(note); await db.SaveChangesAsync(ct); return true;
    }
    private static CompanyNoteDto Map(CompanyNote n) => new() { Id = n.Id, CompanyName = n.CompanyName, Notes = n.Notes, Rating = n.Rating, Website = n.Website, Industry = n.Industry, Products = n.Products, TechnologyStack = n.TechnologyStack, Culture = n.Culture, InterviewPreparation = n.InterviewPreparation, CreatedAt = n.CreatedAt, UpdatedAt = n.UpdatedAt };
}
