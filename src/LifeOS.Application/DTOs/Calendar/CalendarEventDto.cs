namespace LifeOS.Application.DTOs.Calendar;

public class CalendarEventDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset? EndTime { get; set; }
    public bool IsAllDay { get; set; }
    public string? Location { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string? Color { get; set; }
    public bool IsRecurring { get; set; }
    public Guid? HabitId { get; set; }
    public string? HabitName { get; set; }
    public Guid? JobId { get; set; }
    public string? JobTitle { get; set; }
}

public class CreateCalendarEventRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset? EndTime { get; set; }
    public bool IsAllDay { get; set; }
    public string? Location { get; set; }
    public string EventType { get; set; } = "general";
    public string? Color { get; set; }
    public Guid? HabitId { get; set; }
    public Guid? JobId { get; set; }
}

public class UpdateCalendarEventRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset? EndTime { get; set; }
    public bool IsAllDay { get; set; }
    public string? Location { get; set; }
    public string EventType { get; set; } = "general";
    public string? Color { get; set; }
}

public class WeekViewDto
{
    public DateTimeOffset WeekStart { get; set; }
    public List<CalendarEventDto> Events { get; set; } = new();
}
