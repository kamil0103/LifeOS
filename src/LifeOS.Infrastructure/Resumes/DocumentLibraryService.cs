using LifeOS.Application.DTOs.Documents;
using LifeOS.Application.Interfaces;
using LifeOS.Application.Services;
using LifeOS.Domain.Entities;
using LifeOS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
namespace LifeOS.Infrastructure.Resumes;
public class DocumentLibraryService(AppDbContext db, IDocumentStorage storage) : IDocumentLibraryService
{
    private IQueryable<Document> Query(Guid userId) => db.Documents.Where(d => d.UserId == userId).Include(d => d.Job).Include(d => d.Application).ThenInclude(a => a!.Job).Include(d => d.SourceResumeVersion);
    public async Task<List<GeneratedDocumentDto>> ListAsync(Guid userId, CancellationToken ct) => (await Query(userId).AsNoTracking().OrderByDescending(d => d.GeneratedAt).ToListAsync(ct)).Select(Map).ToList();
    public async Task<byte[]?> PreviewAsync(Guid userId, Guid id, CancellationToken ct)
    {
        var document = await db.Documents.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id && d.UserId == userId, ct); if (document == null) return null;
        byte[] bytes; try { bytes = await storage.LoadAsync(document.StoragePath, ct); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { throw new InvalidOperationException("The stored PDF could not be loaded. Try downloading or regenerating the document."); }
        if (bytes.Length < 5 || !bytes.AsSpan(0, 5).SequenceEqual("%PDF-"u8)) throw new InvalidOperationException("This stored file is not a supported PDF preview. Download or regenerate it.");
        return bytes;
    }
    public async Task<DocumentLinkTargets> ResolveLinksAsync(Guid userId, DocumentLinksRequest r, CancellationToken ct)
    {
        var jobId = r.JobId;
        if (r.ApplicationId.HasValue) {
            var application = await db.JobApplications.AsNoTracking().FirstOrDefaultAsync(a => a.Id == r.ApplicationId && a.UserId == userId, ct) ?? throw new InvalidOperationException("Choose one of your own applications.");
            if (jobId.HasValue && jobId != application.JobId) throw new InvalidOperationException("The job must match the selected application's job.");
            jobId = application.JobId;
        }
        if (jobId.HasValue && !await db.Jobs.AnyAsync(j => j.Id == jobId && j.UserId == userId, ct)) throw new InvalidOperationException("Choose one of your own saved jobs.");
        if (r.SourceResumeVersionId.HasValue && !await db.ResumeVersions.AnyAsync(v => v.Id == r.SourceResumeVersionId && v.UserId == userId, ct)) throw new InvalidOperationException("Choose one of your own resume versions.");
        return new(jobId, r.ApplicationId, r.SourceResumeVersionId);
    }
    public async Task<GeneratedDocumentDto?> UpdateLinksAsync(Guid userId, Guid id, DocumentLinksRequest r, CancellationToken ct)
    {
        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
        if (tx != null) { var key = BitConverter.ToInt64(userId.ToByteArray(), 0) ^ 0x444F43554D45L; await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({key})", ct); }
        var doc = await db.Documents.FirstOrDefaultAsync(d => d.Id == id && d.UserId == userId, ct); if (doc == null) return null;
        if (!r.ExpectedUpdatedAt.HasValue || r.ExpectedUpdatedAt.Value.ToUnixTimeMilliseconds() != doc.UpdatedAt.ToUnixTimeMilliseconds()) throw new RecordConflictException("Document links changed since you opened them. Your edits are retained; reopen the latest document before saving again.");
        var links = await ResolveLinksAsync(userId, r, ct); doc.JobId = links.JobId; doc.ApplicationId = links.ApplicationId; doc.SourceResumeVersionId = links.SourceResumeVersionId; doc.UpdatedAt = DateTimeOffset.FromUnixTimeMilliseconds(Math.Max(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), doc.UpdatedAt.ToUnixTimeMilliseconds() + 1));
        await db.SaveChangesAsync(ct); if (tx != null) await tx.CommitAsync(ct);
        // Resolve fresh navigations after changing the foreign keys.
        return (await ListAsync(userId, ct)).Single(d => d.Id == id);
    }
    private static GeneratedDocumentDto Map(Document d) => new() { Id = d.Id, Type = d.Type, Filename = d.Filename, GeneratedAt = d.GeneratedAt, UpdatedAt = d.UpdatedAt, JobId = d.JobId, JobTitle = d.Job?.UserId == d.UserId ? d.Job.Title : null, ApplicationId = d.ApplicationId, ApplicationCompany = d.Application?.UserId == d.UserId && d.Application.Job.UserId == d.UserId ? d.Application.Job.Company : null, ApplicationStatus = d.Application?.UserId == d.UserId ? d.Application.Status : null, SourceResumeVersionId = d.SourceResumeVersionId, SourceResumeVersionTitle = d.SourceResumeVersion?.UserId == d.UserId ? d.SourceResumeVersion.Title : null };
}
