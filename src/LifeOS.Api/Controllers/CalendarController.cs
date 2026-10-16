using LifeOS.Application.DTOs.Calendar;
using LifeOS.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace LifeOS.Api.Controllers;
[ApiController, Route("api/[controller]"), Authorize]
public class CalendarController(ICalendarService calendar, ICurrentUserService user, ILocalDayService days) : ControllerBase
{
    private Guid UserId => user.UserId ?? throw new UnauthorizedAccessException();
    [HttpGet("events")]
    public async Task<ActionResult<List<CalendarEventDto>>> GetEvents([FromQuery] DateTimeOffset? start, [FromQuery] DateTimeOffset? end, CancellationToken ct) => await calendar.GetAsync(UserId,start,end,ct);
    [HttpPost("events")]
    public async Task<ActionResult<CalendarEventDto>> Create(CreateCalendarEventRequest request,CancellationToken ct) => await calendar.CreateAsync(UserId,request,ct);
    [HttpPut("events/{id:guid}")]
    public async Task<ActionResult<CalendarEventDto>> Update(Guid id,UpdateCalendarEventRequest request,CancellationToken ct)
    { var result=await calendar.UpdateAsync(UserId,id,request,ct); return result == null ? NotFound() : Ok(result); }
    [HttpDelete("events/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id,CancellationToken ct) => await calendar.DeleteAsync(UserId,id,ct) ? NoContent() : NotFound();
    [HttpGet("week")]
    public async Task<ActionResult<WeekViewDto>> Week([FromQuery] DateTimeOffset? date,CancellationToken ct)
    {
        var day=await days.TodayAsync(UserId,ct);
        if(date.HasValue) day=LifeOS.Application.Services.LocalDay.At(date.Value,day.Zone.Id);
        var first=day.Offset(-(int)day.Date.DayOfWeek);
        return new WeekViewDto { WeekStart=first.Start, Events=await calendar.GetAsync(UserId,first.Start,first.Offset(7).Start,ct) };
    }
}
