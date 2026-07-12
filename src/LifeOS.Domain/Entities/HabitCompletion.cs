namespace LifeOS.Domain.Entities;

public class HabitCompletion : BaseEntity
{
    public Guid HabitId { get; set; }
    public Habit Habit { get; set; } = null!;

    public DateTimeOffset CompletedAt { get; set; } = DateTimeOffset.UtcNow;
    public decimal? Value { get; set; }
    public string? Notes { get; set; }
}
