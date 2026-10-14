using LifeOS.Application.DTOs.Planning;
using LifeOS.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace LifeOS.Api.Controllers;
[ApiController,Route("api/planning"),Authorize]
public class PlanningController(IPlanningService planning,IReminderService reminders,ICurrentUserService user) : ControllerBase
{
    private Guid UserId=>user.UserId??throw new UnauthorizedAccessException();
    [HttpGet("today")]
    public async Task<DailyPlanDto> Today(CancellationToken ct){var result=await planning.TodayAsync(UserId,ct);await reminders.RemindAsync(UserId,result,ct);return result;}
    [HttpPost("complete")]
    public async Task<IActionResult> Complete([FromBody]CompleteActionRequest request,CancellationToken ct)=>await planning.CompleteAsync(UserId,request.ActionId,ct)?NoContent():NotFound();
    [HttpPost("schedule")]
    public Task<ScheduledActionResult> Schedule(ScheduleActionRequest request,CancellationToken ct)=>planning.ScheduleAsync(UserId,request,ct);
    [HttpPut("applications/{id:guid}/follow-up")]
    public async Task<IActionResult> FollowUp(Guid id,FollowUpRequest request,CancellationToken ct)=>await planning.FollowUpAsync(UserId,id,request,ct)?NoContent():NotFound();
    [HttpPut("applications/{id:guid}/details")]
    public async Task<IActionResult> Details(Guid id,ApplicationDetailsRequest request,CancellationToken ct)=>await planning.DetailsAsync(UserId,id,request,ct)?NoContent():NotFound();
}
public record CompleteActionRequest(string ActionId);
