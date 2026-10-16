using LifeOS.Application.DTOs.Jobs;
namespace LifeOS.Application.Interfaces;
public interface ICareerWorkflowService
{
    Task<JobDto?> UpdateJobAsync(Guid userId,Guid jobId,UpdateJobRequest request,CancellationToken ct);
    Task<bool> DeleteJobAsync(Guid userId,Guid jobId,CancellationToken ct);
    Task EnsureAssetUnusedAsync(Guid assetId,bool isResume,CancellationToken ct);
    Task<JobDto> SaveJobAsync(Guid userId,CreateJobRequest request,CancellationToken ct);
    Task<JobApplicationDto> TrackApplicationAsync(Guid userId,CreateApplicationRequest request,CancellationToken ct);
}
