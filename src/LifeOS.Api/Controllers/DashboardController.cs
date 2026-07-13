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

    public DashboardController(AppDbContext context)
    {
        _context = context;
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
        var todayUtc = DateTimeOffset.UtcNow;
        var todayStart = new DateTimeOffset(todayUtc.Year, todayUtc.Month, todayUtc.Day, 0, 0, 0, TimeSpan.Zero);
        var todayEnd = todayStart.AddDays(1);

        // Habits not completed today
        var habits = await _context.Habits
            .AsNoTracking()
            .Where(h => h.UserId == userId && h.IsActive)
            .Include(h => h.Streak)
            .Select(h => new {
                h.Id,
                h.Name,
                h.Category,
                h.Icon,
                h.Color,
                h.Streak!.CurrentStreak,
                CompletedToday = _context.HabitCompletions.Any(c => c.HabitId == h.Id && c.CompletedAt >= todayStart && c.CompletedAt < todayEnd)
            })
            .ToListAsync(ct);

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
        var last7DaysStart = todayStart.AddDays(-6);

        var completionsByDay = await _context.HabitCompletions
            .AsNoTracking()
            .Where(c => c.Habit.UserId == userId && c.CompletedAt >= last7DaysStart && c.CompletedAt < todayEnd)
            .ToListAsync(ct);

        var last7Days = Enumerable.Range(0, 7)
            .Select(i => todayStart.AddDays(-i))
            .ToList();

        var weeklyProgress = last7Days.Select(d => new DayProgressDto
        {
            Date = d.DateTime,
            DayName = d.ToString("ddd"),
            CompletionCount = completionsByDay.Count(c => c.CompletedAt >= d && c.CompletedAt < d.AddDays(1))
        }).ToList();

        return Ok(new TodayDashboardDto
        {
            Date = todayStart.DateTime,
            Habits = habits.Select(h => new TodayHabitDto
            {
                Id = h.Id,
                Name = h.Name,
                Category = h.Category,
                Icon = h.Icon,
                Color = h.Color,
                CurrentStreak = h.CurrentStreak,
                IsCompleted = h.CompletedToday
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
        var today = DateTimeOffset.UtcNow.Date;

        var totalHabits = await _context.Habits.CountAsync(h => h.UserId == userId && h.IsActive, ct);
        var completedToday = await _context.HabitCompletions
            .CountAsync(c => c.Habit.UserId == userId && c.CompletedAt.Date == today, ct);
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
