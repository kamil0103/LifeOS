using LifeOS.Application.DTOs.Jobs;
namespace LifeOS.Application.Interfaces;
public interface IApplicationInterviewService
{
    Task<List<ApplicationInterviewDto>?> ListAsync(Guid userId, Guid applicationId, CancellationToken ct);
    Task<ApplicationInterviewDto?> SaveAsync(Guid userId, Guid applicationId, Guid? id, SaveInterviewRequest request, CancellationToken ct);
    Task<bool> DeleteApplicationAsync(Guid userId, Guid applicationId, CancellationToken ct);
}
