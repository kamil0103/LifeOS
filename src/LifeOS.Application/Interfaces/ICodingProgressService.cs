using LifeOS.Application.DTOs.Coding;
namespace LifeOS.Application.Interfaces;
public interface ICodingProgressService
{
    Task<CodingProblemDto> CreateAsync(Guid userId, CreateCodingProblemRequest request, CancellationToken ct);
    Task<CodingProblemDto?> UpdateAsync(Guid userId, Guid id, UpdateCodingProblemRequest request, CancellationToken ct);
    Task<bool> DeleteAsync(Guid userId, Guid id, CancellationToken ct);
    Task<CodingProblemDto?> RecordAsync(Guid userId, Guid id, SolveProblemRequest request, bool review, CancellationToken ct);
    Task<CodingProblemDto?> UnsolveAsync(Guid userId, Guid id, CancellationToken ct);
}
