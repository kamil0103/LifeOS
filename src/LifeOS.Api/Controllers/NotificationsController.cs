using LifeOS.Application.DTOs.Planning;
using LifeOS.Application.Interfaces;
using LifeOS.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace LifeOS.Api.Controllers;
[ApiController,Route("api/notifications"),Authorize]
public class NotificationsController(IReminderService reminders,ICurrentUserService user) : ControllerBase
{
    private Guid UserId=>user.UserId??throw new UnauthorizedAccessException();
    [HttpGet] public Task<List<Notification>> List([FromQuery]bool unread,[FromQuery]int page=1,CancellationToken ct=default)=>reminders.ListAsync(UserId,unread,page,ct);
    [HttpGet("count")] public async Task<object> Count(CancellationToken ct)=>new{unread=await reminders.CountAsync(UserId,ct)};
    [HttpPost("{id:guid}/read")] public async Task<IActionResult> Read(Guid id,CancellationToken ct)=>await reminders.ReadAsync(UserId,id,ct)?NoContent():NotFound();
    [HttpPost("read-all")] public async Task<IActionResult> ReadAll(CancellationToken ct){await reminders.ReadAllAsync(UserId,ct);return NoContent();}
    [HttpDelete("{id:guid}")] public async Task<IActionResult> Delete(Guid id,CancellationToken ct)=>await reminders.ArchiveAsync(UserId,id,ct)?NoContent():NotFound();
    [HttpPost("{id:guid}/snooze")] public async Task<IActionResult> Snooze(Guid id,SnoozeRequest request,CancellationToken ct)=>await reminders.SnoozeAsync(UserId,id,request.Minutes,ct)?NoContent():NotFound();
    [HttpGet("preferences")] public Task<ReminderPreferencesDto> Preferences(CancellationToken ct)=>reminders.PreferencesAsync(UserId,ct);
    [HttpPut("preferences")] public async Task<IActionResult> Preferences(ReminderPreferencesDto request,CancellationToken ct){await reminders.SavePreferencesAsync(UserId,request,ct);return NoContent();}
}
public record SnoozeRequest(int Minutes);
