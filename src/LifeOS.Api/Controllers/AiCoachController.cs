using LifeOS.Application.DTOs.Coach;
using LifeOS.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace LifeOS.Api.Controllers;
[ApiController,Route("api/aicoach"),Authorize]
public class AiCoachController(IAiCoachService coach,ICurrentUserService user) : ControllerBase
{
    private Guid UserId=>user.UserId??throw new UnauthorizedAccessException();
    [HttpPost("generate-mission")] public Task<DailyMissionDto> GenerateMission(CancellationToken ct)=>coach.GenerateMissionAsync(UserId,ct);
    [HttpGet("mission")] public async Task<ActionResult<DailyMissionDto>> Mission(CancellationToken ct){var m=await coach.MissionAsync(UserId,ct);return m==null?NotFound():Ok(m);}
    [HttpPost("mission/{id:guid}/complete")] public async Task<ActionResult<DailyMissionDto>> Complete(Guid id,CancellationToken ct){var m=await coach.CompleteMissionAsync(UserId,id,ct);return m==null?NotFound():Ok(m);}
    [HttpGet("messages")] public Task<List<AiCoachMessageDto>> Messages([FromQuery]int limit=10,CancellationToken ct=default)=>coach.InsightsAsync(UserId,limit,ct);
    [HttpPost("messages/{id:guid}/read")] public async Task<IActionResult> Read(Guid id,CancellationToken ct)=>await coach.ReadInsightAsync(UserId,id,ct)?NoContent():NotFound();
    [HttpPost("chat")] public Task<CoachChatReply> Chat(CoachChatRequest request,CancellationToken ct)=>coach.ChatAsync(UserId,request,ct);
    [HttpGet("history")] public Task<List<CoachHistoryItem>> History(CancellationToken ct)=>coach.HistoryAsync(UserId,ct);
    [HttpDelete("history")] public async Task<IActionResult> Clear(CancellationToken ct){await coach.ClearHistoryAsync(UserId,ct);return NoContent();}
    [HttpGet("context")] public Task<CoachContext> Context(CancellationToken ct)=>coach.ContextAsync(UserId,ct);
    [HttpGet("preferences")] public Task<CoachPreferences> Preferences(CancellationToken ct)=>coach.PreferencesAsync(UserId,ct);
    [HttpPut("preferences")] public async Task<IActionResult> Preferences(CoachPreferences request,CancellationToken ct){await coach.SavePreferencesAsync(UserId,request,ct);return NoContent();}
    [HttpGet("memories")] public Task<List<CoachMemoryDto>> Memories(CancellationToken ct)=>coach.MemoriesAsync(UserId,ct);
    [HttpPost("memories")] public async Task<ActionResult<CoachMemoryDto>> Memory(CoachMemoryRequest request,CancellationToken ct)=>Ok(await coach.SaveMemoryAsync(UserId,null,request,ct));
    [HttpPut("memories/{id:guid}")] public async Task<ActionResult<CoachMemoryDto>> Memory(Guid id,CoachMemoryRequest request,CancellationToken ct){var m=await coach.SaveMemoryAsync(UserId,id,request,ct);return m==null?NotFound():Ok(m);}
    [HttpDelete("memories/{id:guid}")] public async Task<IActionResult> DeleteMemory(Guid id,CancellationToken ct)=>await coach.DeleteMemoryAsync(UserId,id,ct)?NoContent():NotFound();
}
