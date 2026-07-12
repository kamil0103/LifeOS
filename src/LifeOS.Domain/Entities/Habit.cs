namespace LifeOS.Domain.Entities;

public class Habit : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty; // coding, exercise, bible, prayer, etc.
    public decimal? TargetValue { get; set; }
    public string? Unit { get; set; }
    public string Frequency { get; set; } = "daily";
    public string? Icon { get; set; }
    public string? Color { get; set; }
    public bool IsActive { get; set; } = true;

    public List<HabitCompletion> Completions { get; set; } = [];
    public Streak? Streak { get; set; }
}
