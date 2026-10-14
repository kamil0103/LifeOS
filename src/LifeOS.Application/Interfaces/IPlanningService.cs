using LifeOS.Application.DTOs.Planning;
namespace LifeOS.Application.Interfaces;
public interface IPlanningService
{
    Task<DailyPlanDto> TodayAsync(Guid userId, CancellationToken ct);
    Task<bool> CompleteAsync(Guid userId,string actionId,CancellationToken ct);
    Task<ScheduledActionResult> ScheduleAsync(Guid userId,ScheduleActionRequest request,CancellationToken ct);
    Task<bool> FollowUpAsync(Guid userId,Guid applicationId,FollowUpRequest request,CancellationToken ct);
    Task<bool> DetailsAsync(Guid userId,Guid applicationId,ApplicationDetailsRequest request,CancellationToken ct);
}
