using LifeOS.Application.DTOs.Habits;
using LifeOS.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace LifeOS.Api.Controllers;
[ApiController,Route("api/[controller]"),Authorize]
public class HabitsController(IHabitService habits) : ControllerBase
{
    private Guid UserId=>Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value??User.FindFirst("sub")!.Value);
    [HttpGet("today")] public async Task<ActionResult<List<HabitDto>>> Today(CancellationToken ct)=>Ok(await habits.ListAsync(UserId,false,true,ct));
    [HttpGet] public async Task<ActionResult<List<HabitDto>>> List([FromQuery]bool archived=false,CancellationToken ct=default)=>Ok(await habits.ListAsync(UserId,archived,false,ct));
    [HttpPost] public async Task<ActionResult<HabitDto>> Create(HabitSaveRequest request,CancellationToken ct)=>Ok(await habits.SaveAsync(UserId,null,request,ct));
    [HttpPut("{id:guid}")] public async Task<ActionResult<HabitDto>> Update(Guid id,HabitSaveRequest request,CancellationToken ct){var result=await habits.SaveAsync(UserId,id,request,ct);return result==null?NotFound():Ok(result);}
    [HttpDelete("{id:guid}")] public async Task<IActionResult> Archive(Guid id,CancellationToken ct)=>await habits.SetActiveAsync(UserId,id,false,ct)?NoContent():NotFound();
    [HttpPost("{id:guid}/restore")] public async Task<IActionResult> Restore(Guid id,CancellationToken ct)=>await habits.SetActiveAsync(UserId,id,true,ct)?NoContent():NotFound();
    [HttpPost("{id:guid}/complete")] public async Task<ActionResult<HabitCompletionResult>> Complete(Guid id,[FromBody]HabitCompletionRequest? request,CancellationToken ct){var result=await habits.ProgressAsync(UserId,id,request,true,ct);return result==null?NotFound():Ok(result);}
    [HttpPost("{id:guid}/progress")] public async Task<ActionResult<HabitCompletionResult>> Progress(Guid id,HabitCompletionRequest request,CancellationToken ct){var result=await habits.ProgressAsync(UserId,id,request,false,ct);return result==null?NotFound():Ok(result);}
    [HttpGet("{id:guid}/completions")] public async Task<ActionResult<List<HabitHistoryDto>>> History(Guid id,[FromQuery]int days=30,CancellationToken ct=default){var result=await habits.HistoryAsync(UserId,id,days,ct);return result==null?NotFound():Ok(result);}
    [HttpGet("streaks")] public async Task<ActionResult<List<StreakDto>>> Streaks(CancellationToken ct)=>Ok(await habits.StreaksAsync(UserId,ct));
}
