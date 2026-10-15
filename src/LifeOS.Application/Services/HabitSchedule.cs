using LifeOS.Domain.Entities;
namespace LifeOS.Application.Services;
public static class HabitSchedule
{
    public static bool IsDue(Habit habit, LocalDay day) => habit.IsActive && (habit.ScheduleDaysMask & (1 << (int)day.Date.DayOfWeek)) != 0;
    public static LocalDay PreviousDue(Habit habit, LocalDay day)
    {
        for (var i = 1; i <= 7; i++) { var previous = day.Offset(-i); if ((habit.ScheduleDaysMask & (1 << (int)previous.Date.DayOfWeek)) != 0) return previous; }
        return day.Offset(-1);
    }
    public static int CurrentStreak(Habit habit, LocalDay day) => habit.Streak?.LastCompletedAt >= PreviousDue(habit, day).Start ? habit.Streak.CurrentStreak : 0;
}
