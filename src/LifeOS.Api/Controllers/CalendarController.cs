using LifeOS.Application.DTOs.Calendar;
using LifeOS.Domain.Entities;
using LifeOS.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CalendarController : ControllerBase
{
    private readonly AppDbContext _context;

    public CalendarController(AppDbContext context)
    {
        _context = context;
    }

    private Guid GetUserId()
    {
        var sub = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        return Guid.Parse(sub!);
    }

    [HttpGet("events")]
    public async Task<ActionResult<List<CalendarEventDto>>> GetEvents(
        [FromQuery] DateTimeOffset? start,
        [FromQuery] DateTimeOffset? end,
        CancellationToken ct)
    {
        var userId = GetUserId();
        var query = _context.CalendarEvents
            .AsNoTracking()
            .Where(e => e.UserId == userId)
            .AsQueryable();

        if (start.HasValue)
            query = query.Where(e => e.StartTime >= start.Value);
        if (end.HasValue)
            query = query.Where(e => e.StartTime <= end.Value);

        var events = await query
            .Include(e => e.Habit)
            .Include(e => e.Job)
            .OrderBy(e => e.StartTime)
            .Select(e => new CalendarEventDto
            {
                Id = e.Id,
                Title = e.Title,
                Description = e.Description,
                StartTime = e.StartTime,
                EndTime = e.EndTime,
                IsAllDay = e.IsAllDay,
                Location = e.Location,
                EventType = e.EventType,
                Color = e.Color,
                IsRecurring = e.IsRecurring,
                HabitId = e.HabitId,
                HabitName = e.Habit != null ? e.Habit.Name : null,
                JobId = e.JobId,
                JobTitle = e.Job != null ? e.Job.Title : null
            })
            .ToListAsync(ct);

        return events;
    }

    [HttpPost("events")]
    public async Task<ActionResult<CalendarEventDto>> CreateEvent([FromBody] CreateCalendarEventRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        var evt = new CalendarEvent
        {
            UserId = userId,
            Title = request.Title,
            Description = request.Description,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            IsAllDay = request.IsAllDay,
            Location = request.Location,
            EventType = request.EventType,
            Color = request.Color,
            HabitId = request.HabitId,
            JobId = request.JobId
        };

        _context.CalendarEvents.Add(evt);
        await _context.SaveChangesAsync(ct);

        var dto = await GetEventDto(evt.Id, ct);
        if (dto == null) return NotFound();
        return dto;
    }

    [HttpPut("events/{id}")]
    public async Task<ActionResult<CalendarEventDto>> UpdateEvent(Guid id, [FromBody] UpdateCalendarEventRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        var evt = await _context.CalendarEvents.FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId, ct);
        if (evt == null)
            return NotFound();

        evt.Title = request.Title;
        evt.Description = request.Description;
        evt.StartTime = request.StartTime;
        evt.EndTime = request.EndTime;
        evt.IsAllDay = request.IsAllDay;
        evt.Location = request.Location;
        evt.EventType = request.EventType;
        evt.Color = request.Color;

        await _context.SaveChangesAsync(ct);
        var dto = await GetEventDto(evt.Id, ct);
        if (dto == null) return NotFound();
        return dto;
    }

    [HttpDelete("events/{id}")]
    public async Task<IActionResult> DeleteEvent(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        var evt = await _context.CalendarEvents.FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId, ct);
        if (evt == null)
            return NotFound();

        _context.CalendarEvents.Remove(evt);
        await _context.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpGet("week")]
    public async Task<ActionResult<WeekViewDto>> GetWeekView([FromQuery] DateTimeOffset? date, CancellationToken ct)
    {
        var targetDate = date ?? DateTimeOffset.UtcNow;
        var weekStart = targetDate.AddDays(-(int)targetDate.DayOfWeek).Date;
        var weekEnd = weekStart.AddDays(7);

        var events = await _context.CalendarEvents
            .AsNoTracking()
            .Where(e => e.UserId == GetUserId() && e.StartTime >= weekStart && e.StartTime < weekEnd)
            .Include(e => e.Habit)
            .Include(e => e.Job)
            .OrderBy(e => e.StartTime)
            .Select(e => new CalendarEventDto
            {
                Id = e.Id,
                Title = e.Title,
                Description = e.Description,
                StartTime = e.StartTime,
                EndTime = e.EndTime,
                IsAllDay = e.IsAllDay,
                Location = e.Location,
                EventType = e.EventType,
                Color = e.Color,
                IsRecurring = e.IsRecurring,
                HabitId = e.HabitId,
                HabitName = e.Habit != null ? e.Habit.Name : null,
                JobId = e.JobId,
                JobTitle = e.Job != null ? e.Job.Title : null
            })
            .ToListAsync(ct);

        return new WeekViewDto
        {
            WeekStart = weekStart,
            Events = events
        };
    }

    private async Task<CalendarEventDto?> GetEventDto(Guid id, CancellationToken ct)
    {
        return await _context.CalendarEvents
            .AsNoTracking()
            .Include(e => e.Habit)
            .Include(e => e.Job)
            .Where(e => e.Id == id)
            .Select(e => new CalendarEventDto
            {
                Id = e.Id,
                Title = e.Title,
                Description = e.Description,
                StartTime = e.StartTime,
                EndTime = e.EndTime,
                IsAllDay = e.IsAllDay,
                Location = e.Location,
                EventType = e.EventType,
                Color = e.Color,
                IsRecurring = e.IsRecurring,
                HabitId = e.HabitId,
                HabitName = e.Habit != null ? e.Habit.Name : null,
                JobId = e.JobId,
                JobTitle = e.Job != null ? e.Job.Title : null
            })
            .FirstOrDefaultAsync(ct);
    }
}
