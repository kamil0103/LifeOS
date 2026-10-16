namespace LifeOS.Application.DTOs.Habits;
public class HabitSaveRequest
{
    public string Name { get; set; } = "";
    public string Category { get; set; } = "Other";
    public decimal? TargetValue { get; set; }
    public string? Unit { get; set; }
    public string? Frequency { get; set; }
    public int? ScheduleDaysMask { get; set; }
    public string? Icon { get; set; }
    public string? Color { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset? ExpectedUpdatedAt { get; set; }
}
public class HabitDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public decimal? TargetValue { get; set; }
    public string? Unit { get; set; }
    public string? Icon { get; set; }
    public string? Color { get; set; }
    public string TimeZone { get; set; } = "";
    public string Frequency { get; set; } = "daily";
    public int ScheduleDaysMask { get; set; }
    public bool IsActive { get; set; }
    public bool IsDueToday { get; set; }
    public bool IsCompleted { get; set; }
    public decimal TodayValue { get; set; }
    public int CurrentStreak { get; set; }
    public int LongestStreak { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
public class HabitCompletionRequest { public decimal? Value { get; set; } public string? Notes { get; set; } }
public record HabitCompletionResult(int CurrentStreak, int LongestStreak, int XpEarned, int TotalXp, bool IsCompleted, decimal TodayValue);
public record HabitHistoryDto(Guid Id, DateTimeOffset CompletedAt, decimal? Value, string? Notes, bool IsCompleted);
public record StreakDto(Guid HabitId, string HabitName, int CurrentStreak, int LongestStreak, DateTimeOffset? LastCompletedAt);
