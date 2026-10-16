using System.Text;
using System.Text.Json;
using LifeOS.Application.DTOs.Documents;
using LifeOS.Application.Interfaces;
using LifeOS.Domain.Entities;
using LifeOS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
namespace LifeOS.Infrastructure.Resumes;
public class ResumeDraftService(AppDbContext db, IResumeGenerator generator) : IResumeDraftService
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    public async Task<ResumeDraftDto?> GetAsync(Guid userId, CancellationToken ct)
    {
        var draft = await db.ResumeDrafts.AsNoTracking().FirstOrDefaultAsync(d => d.UserId == userId, ct);
        return draft == null ? null : Map(draft);
    }
    private static string Serialize(ResumeDataDto data)
    {
        if (data == null) throw new InvalidOperationException("Resume data is required.");
        if ((data.Title?.Length ?? 0) > 200 || (data.Template?.Length ?? 0) > 50) throw new InvalidOperationException("Resume title must be at most 200 characters and template at most 50.");
        data.Profile ??= new(); data.SectionOrder ??= []; data.Experience ??= []; data.Education ??= []; data.Skills ??= []; data.Projects ??= []; data.Certifications ??= []; data.Courses ??= []; data.PendingRecommendations ??= [];
        if (data.Experience.Any(x => x == null) || data.Education.Any(x => x == null) || data.Skills.Any(x => x == null) || data.Projects.Any(x => x == null) || data.Certifications.Any(x => x == null) || data.Courses.Any(x => x == null)) throw new InvalidOperationException("Remove empty resume rows before saving.");
        foreach (var row in data.Experience) row.Bullets ??= "";
        var json = JsonSerializer.Serialize(data); if (Encoding.UTF8.GetByteCount(json) > 512000) throw new InvalidOperationException("Resume draft must be at most 500 KB."); return json;
    }
    public async Task<ResumeDraftDto> SaveAsync(Guid userId, SaveResumeDraftRequest request, CancellationToken ct)
    {
        var json = Serialize(request.Data);
        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
        if (tx != null) { var key = BitConverter.ToInt64(userId.ToByteArray(), 0) ^ 0x524553554D45L; await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({key})", ct); }
        var draft = await db.ResumeDrafts.FirstOrDefaultAsync(d => d.UserId == userId, ct);
        // An identical retry never overwrites a newer value or creates another version.
        if (draft != null && draft.ContentJson == json) { if (tx != null) await tx.CommitAsync(ct); return Map(draft); }
        if ((draft == null && request.ExpectedRevision.HasValue) || (draft != null && request.ExpectedRevision != draft.Revision)) throw new RecordConflictException("A newer resume draft was saved in another tab or device. Your local edits are retained. Download or save them as a version before reloading the server draft.");
        if (draft == null) { draft = new() { UserId = userId }; db.ResumeDrafts.Add(draft); }
        draft.ContentJson = json; draft.Revision = Guid.NewGuid(); draft.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct); if (tx != null) await tx.CommitAsync(ct); return Map(draft);
    }
    public Task<byte[]> PreviewAsync(ResumeDataDto data, CancellationToken ct)
    {
        Serialize(data); if (string.IsNullOrWhiteSpace(data.Template)) data.Template = "harvard";
        if (data.SectionOrder.Count == 0) data.SectionOrder = ["education", "experience", "skills", "projects", "certifications"];
        return generator.GenerateResumePdfAsync(data, data.Template, ct);
    }
    private static ResumeDraftDto Map(ResumeDraft draft) => new(draft.Revision, draft.UpdatedAt, JsonSerializer.Deserialize<ResumeDataDto>(draft.ContentJson, Json)!);
}
