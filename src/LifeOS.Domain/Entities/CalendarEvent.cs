namespace LifeOS.Domain.Entities;

public class CalendarEvent : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset? EndTime { get; set; }
    public bool IsAllDay { get; set; } = false;
    public string? Location { get; set; }
    public string EventType { get; set; } = "general"; // general, habit, job, coding, meeting
    public string? Color { get; set; }
    public bool IsRecurring { get; set; } = false;
    public string? RecurrenceRule { get; set; }
    public Guid? HabitId { get; set; }
    public Habit? Habit { get; set; }
    public Guid? JobId { get; set; }
    public Job? Job { get; set; }
}
