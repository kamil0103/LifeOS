using LifeOS.Application.Interfaces;
using LifeOS.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILocalDayService _days;
    private readonly IHabitService _habits;

    public DashboardController(AppDbContext context, ILocalDayService days, IHabitService habits)
    {
        _context = context;
        _habits = habits;
        _days = days;
    }

    private Guid GetUserId()
    {
        var sub = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        return Guid.Parse(sub!);
    }

    [HttpGet("today")]
    public async Task<ActionResult<TodayDashboardDto>> GetToday(CancellationToken ct)
    {
        var userId = GetUserId();
        var day = await _days.TodayAsync(userId, ct);
        var todayStart = day.Start;
        var todayEnd = day.End;

        var habits = await _habits.ListAsync(userId, false, true, ct);

        // Job stats
        var jobStats = await _context.Jobs
            .AsNoTracking()
            .Where(j => j.UserId == userId)
            .GroupBy(j => 1)
            .Select(g => new {
                TotalSaved = g.Count(j => j.Status == "saved"),
                TotalApplied = g.Count(j => j.Status == "applied"),
                TotalInterview = g.Count(j => j.Status == "interview"),
                TotalOffer = g.Count(j => j.Status == "offer")
            })
            .FirstOrDefaultAsync(ct);

        // XP
        var totalXp = await _context.XpTransactions
            .Where(x => x.UserId == userId)
            .SumAsync(x => x.Amount, ct);

        var level = CalculateLevel(totalXp);
        var xpForNextLevel = GetXpForLevel(level + 1);
        var xpInCurrentLevel = totalXp - GetXpForLevel(level);

        // Recent completions (last 7 days for heatmap)
        var last7DaysStart = day.Offset(-6).Start;

        var completionsByDay = await _context.HabitCompletions
            .AsNoTracking()
            .Where(c => c.Habit.UserId == userId && c.IsCompleted && c.CompletedAt >= last7DaysStart && c.CompletedAt < todayEnd)
            .ToListAsync(ct);

        var last7Days = Enumerable.Range(0, 7)
            .Select(i => day.Offset(-i))
            .ToList();

        var weeklyProgress = last7Days.Select(d => new DayProgressDto
        {
            Date = d.Date,
            DayName = d.Date.ToString("ddd"),
            CompletionCount = completionsByDay.Where(c => c.CompletedAt >= d.Start && c.CompletedAt < d.End).Select(c => c.HabitId).Distinct().Count()
        }).ToList();

        return Ok(new TodayDashboardDto
        {
            Date = day.Date,
            Habits = habits.Select(h => new TodayHabitDto
            {
                Id = h.Id,
                Name = h.Name,
                Category = h.Category,
                Icon = h.Icon,
                Color = h.Color,
                CurrentStreak = h.CurrentStreak,
                IsCompleted = h.IsCompleted
            }).ToList(),
            JobStats = new JobStatsDto
            {
                Saved = jobStats?.TotalSaved ?? 0,
                Applied = jobStats?.TotalApplied ?? 0,
                Interview = jobStats?.TotalInterview ?? 0,
                Offer = jobStats?.TotalOffer ?? 0
            },
            TotalXp = totalXp,
            Level = level,
            XpForNextLevel = xpForNextLevel - GetXpForLevel(level),
            XpInCurrentLevel = xpInCurrentLevel,
            WeeklyProgress = weeklyProgress,
            TotalStreakDays = habits.Any() ? habits.Max(h => h.CurrentStreak) : 0
        });
    }

    [HttpGet("stats")]
    public async Task<ActionResult<UserStatsDto>> GetStats(CancellationToken ct)
    {
        var userId = GetUserId();
        var day = await _days.TodayAsync(userId, ct);
        var start = day.Start;
        var end = day.End;

        var dueHabits = await _habits.ListAsync(userId, false, true, ct);
        var totalHabits = dueHabits.Count;
        var completedToday = dueHabits.Count(h=>h.IsCompleted);
        var totalXp = await _context.XpTransactions
            .Where(x => x.UserId == userId)
            .SumAsync(x => x.Amount, ct);
        var totalApplications = await _context.JobApplications
            .CountAsync(a => a.UserId == userId, ct);

        return Ok(new UserStatsDto
        {
            TotalHabits = totalHabits,
            CompletedToday = completedToday,
            TotalXp = totalXp,
            Level = CalculateLevel(totalXp),
            TotalApplications = totalApplications
        });
    }

    private static int CalculateLevel(int totalXp)
    {
        int level = 1;
        int xpNeeded = 100;
        int remainingXp = totalXp;

        while (remainingXp >= xpNeeded)
        {
            remainingXp -= xpNeeded;
            level++;
            xpNeeded = (int)(xpNeeded * 1.2);
        }

        return level;
    }

    private static int GetXpForLevel(int level)
    {
        int total = 0;
        int xpNeeded = 100;
        for (int i = 1; i < level; i++)
        {
            total += xpNeeded;
            xpNeeded = (int)(xpNeeded * 1.2);
        }
        return total;
    }
}

public class TodayDashboardDto
{
    public DateTime Date { get; set; }
    public List<TodayHabitDto> Habits { get; set; } = [];
    public JobStatsDto JobStats { get; set; } = new();
    public int TotalXp { get; set; }
    public int Level { get; set; }
    public int XpForNextLevel { get; set; }
    public int XpInCurrentLevel { get; set; }
    public List<DayProgressDto> WeeklyProgress { get; set; } = [];
    public int TotalStreakDays { get; set; }
}

public class TodayHabitDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public string? Color { get; set; }
    public int CurrentStreak { get; set; }
    public bool IsCompleted { get; set; }
}

public class JobStatsDto
{
    public int Saved { get; set; }
    public int Applied { get; set; }
    public int Interview { get; set; }
    public int Offer { get; set; }
}

public class DayProgressDto
{
    public DateTime Date { get; set; }
    public string DayName { get; set; } = string.Empty;
    public int CompletionCount { get; set; }
}

public class UserStatsDto
{
    public int TotalHabits { get; set; }
    public int CompletedToday { get; set; }
    public int TotalXp { get; set; }
    public int Level { get; set; }
    public int TotalApplications { get; set; }
}
