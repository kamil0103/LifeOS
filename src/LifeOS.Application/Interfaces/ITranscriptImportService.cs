using LifeOS.Application.DTOs.Education;
namespace LifeOS.Application.Interfaces;
public record TranscriptImportResult(Guid InstitutionId, Guid? DegreeId, int CoursesAdded, int CoursesSkipped);
public interface ITranscriptImportService
{
    Task<TranscriptImportResult> SaveAsync(Guid userId, SaveExtractedRequest request, CancellationToken ct);
}
