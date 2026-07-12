using LifeOS.Domain.Entities;
using LifeOS.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class HabitsController : ControllerBase
{
    private readonly AppDbContext _context;

    public HabitsController(AppDbContext context)
    {
        _context = context;
    }

    private Guid GetUserId()
    {
        var sub = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        return Guid.Parse(sub!);
    }

    // ==================== HABITS CRUD ====================
    [HttpGet]
    public async Task<ActionResult<List<Habit>>> GetHabits(CancellationToken ct)
    {
        var userId = GetUserId();
        var habits = await _context.Habits
            .AsNoTracking()
            .Where(h => h.UserId == userId && h.IsActive)
            .Include(h => h.Streak)
            .OrderBy(h => h.Category)
            .ThenBy(h => h.Name)
            .ToListAsync(ct);

        return Ok(habits);
    }

    [HttpPost]
    public async Task<ActionResult<Habit>> CreateHabit([FromBody] CreateHabitRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        var habit = new Habit
        {
            UserId = userId,
            Name = request.Name,
            Category = request.Category,
            TargetValue = request.TargetValue,
            Unit = request.Unit,
            Frequency = request.Frequency ?? "daily",
            Icon = request.Icon,
            Color = request.Color,
            IsActive = true
        };

        _context.Habits.Add(habit);
        await _context.SaveChangesAsync(ct);

        // Create initial streak record
        var streak = new Streak { HabitId = habit.Id };
        _context.Streaks.Add(streak);
        await _context.SaveChangesAsync(ct);

        return Ok(habit);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<Habit>> UpdateHabit(Guid id, [FromBody] UpdateHabitRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        var habit = await _context.Habits
            .FirstOrDefaultAsync(h => h.Id == id && h.UserId == userId, ct);

        if (habit == null) return NotFound();

        habit.Name = request.Name;
        habit.Category = request.Category;
        habit.TargetValue = request.TargetValue;
        habit.Unit = request.Unit;
        habit.Frequency = request.Frequency ?? "daily";
        habit.Icon = request.Icon;
        habit.Color = request.Color;
        habit.IsActive = request.IsActive;

        await _context.SaveChangesAsync(ct);
        return Ok(habit);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteHabit(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        var habit = await _context.Habits
            .FirstOrDefaultAsync(h => h.Id == id && h.UserId == userId, ct);

        if (habit == null) return NotFound();

        // Soft delete
        habit.IsActive = false;
        await _context.SaveChangesAsync(ct);
        return NoContent();
    }

    // ==================== COMPLETIONS ====================
    [HttpPost("{id:guid}/complete")]
    public async Task<ActionResult<HabitCompletionResult>> CompleteHabit(Guid id, [FromBody] HabitCompletionRequest? request, CancellationToken ct)
    {
        var userId = GetUserId();
        var habit = await _context.Habits
            .Include(h => h.Streak)
            .FirstOrDefaultAsync(h => h.Id == id && h.UserId == userId, ct);

        if (habit == null) return NotFound();

        // Check if already completed today
        var now = DateTimeOffset.UtcNow;
        var todayStart = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, TimeSpan.Zero);
        var todayEnd = todayStart.AddDays(1);
        var alreadyCompleted = await _context.HabitCompletions
            .AnyAsync(c => c.HabitId == id && c.CompletedAt >= todayStart && c.CompletedAt < todayEnd, ct);

        if (alreadyCompleted)
            return BadRequest("Habit already completed today");

        // Record completion
        var completion = new HabitCompletion
        {
            HabitId = id,
            CompletedAt = DateTimeOffset.UtcNow,
            Value = request?.Value,
            Notes = request?.Notes
        };
        _context.HabitCompletions.Add(completion);

        // Update streak
        var streak = habit.Streak;
        if (streak == null)
        {
            streak = new Streak { HabitId = id };
            _context.Streaks.Add(streak);
        }

        var yesterdayStart = todayStart.AddDays(-1);
        if (streak.LastCompletedAt.HasValue && 
            streak.LastCompletedAt.Value >= yesterdayStart && 
            streak.LastCompletedAt.Value < todayStart)
        {
            streak.CurrentStreak += 1;
        }
        else if (!streak.LastCompletedAt.HasValue || streak.LastCompletedAt.Value < yesterdayStart)
        {
            streak.CurrentStreak = 1;
        }

        if (streak.CurrentStreak > streak.LongestStreak)
            streak.LongestStreak = streak.CurrentStreak;

        streak.LastCompletedAt = DateTimeOffset.UtcNow;

        // Award XP
        var xpAmount = GetXpForCategory(habit.Category);
        var xpTransaction = new XpTransaction
        {
            UserId = userId,
            Amount = xpAmount,
            Source = "habit",
            SourceId = id,
            Description = $"Completed habit: {habit.Name}"
        };
        _context.XpTransactions.Add(xpTransaction);

        await _context.SaveChangesAsync(ct);

        return Ok(new HabitCompletionResult
        {
            CurrentStreak = streak.CurrentStreak,
            LongestStreak = streak.LongestStreak,
            XpEarned = xpAmount,
            TotalXp = await GetTotalXp(userId, ct)
        });
    }

    [HttpGet("{id:guid}/completions")]
    public async Task<ActionResult<List<HabitCompletion>>> GetCompletions(Guid id, [FromQuery] int days = 30, CancellationToken ct = default)
    {
        var userId = GetUserId();
        var since = DateTimeOffset.UtcNow.AddDays(-days);
        
        var completions = await _context.HabitCompletions
            .AsNoTracking()
            .Where(c => c.HabitId == id && c.CompletedAt >= since)
            .OrderByDescending(c => c.CompletedAt)
            .ToListAsync(ct);

        return Ok(completions);
    }

    [HttpGet("streaks")]
    public async Task<ActionResult<List<StreakDto>>> GetStreaks(CancellationToken ct)
    {
        var userId = GetUserId();
        var streaks = await _context.Streaks
            .AsNoTracking()
            .Include(s => s.Habit)
            .Where(s => s.Habit.UserId == userId && s.Habit.IsActive)
            .Select(s => new StreakDto
            {
                HabitId = s.HabitId,
                HabitName = s.Habit.Name,
                CurrentStreak = s.CurrentStreak,
                LongestStreak = s.LongestStreak,
                LastCompletedAt = s.LastCompletedAt
            })
            .ToListAsync(ct);

        return Ok(streaks);
    }

    private async Task<int> GetTotalXp(Guid userId, CancellationToken ct)
    {
        return await _context.XpTransactions
            .Where(x => x.UserId == userId)
            .SumAsync(x => x.Amount, ct);
    }

    private static int GetXpForCategory(string category) => category.ToLower() switch
    {
        "coding" => 25,
        "exercise" => 15,
        "bible" => 20,
        "prayer" => 15,
        "applications" => 30,
        _ => 10
    };
}

public class CreateHabitRequest
{
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal? TargetValue { get; set; }
    public string? Unit { get; set; }
    public string? Frequency { get; set; }
    public string? Icon { get; set; }
    public string? Color { get; set; }
}

public class UpdateHabitRequest
{
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal? TargetValue { get; set; }
    public string? Unit { get; set; }
    public string? Frequency { get; set; }
    public string? Icon { get; set; }
    public string? Color { get; set; }
    public bool IsActive { get; set; } = true;
}

public class HabitCompletionRequest
{
    public decimal? Value { get; set; }
    public string? Notes { get; set; }
}

public class HabitCompletionResult
{
    public int CurrentStreak { get; set; }
    public int LongestStreak { get; set; }
    public int XpEarned { get; set; }
    public int TotalXp { get; set; }
}

public class StreakDto
{
    public Guid HabitId { get; set; }
    public string HabitName { get; set; } = string.Empty;
    public int CurrentStreak { get; set; }
    public int LongestStreak { get; set; }
    public DateTimeOffset? LastCompletedAt { get; set; }
}
