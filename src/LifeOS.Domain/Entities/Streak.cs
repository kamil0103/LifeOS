namespace LifeOS.Domain.Entities;

public class Streak
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid HabitId { get; set; }
    public Habit Habit { get; set; } = null!;

    public int CurrentStreak { get; set; } = 0;
    public int LongestStreak { get; set; } = 0;
    public DateTimeOffset? LastCompletedAt { get; set; }
}
