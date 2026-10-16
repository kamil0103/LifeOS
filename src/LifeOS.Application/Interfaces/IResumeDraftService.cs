using LifeOS.Application.DTOs.Documents;
namespace LifeOS.Application.Interfaces;
public interface IResumeDraftService
{
    Task<ResumeDraftDto?> GetAsync(Guid userId, CancellationToken ct);
    Task<ResumeDraftDto> SaveAsync(Guid userId, SaveResumeDraftRequest request, CancellationToken ct);
    Task<byte[]> PreviewAsync(ResumeDataDto data, CancellationToken ct);
}
