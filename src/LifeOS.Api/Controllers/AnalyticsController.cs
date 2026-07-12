using LifeOS.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AnalyticsController : ControllerBase
{
    private readonly AppDbContext _context;

    public AnalyticsController(AppDbContext context)
    {
        _context = context;
    }

    private Guid GetUserId()
    {
        var sub = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        return Guid.Parse(sub!);
    }

    [HttpGet("xp-trends")]
    public async Task<ActionResult<List<object>>> GetXpTrends([FromQuery] int days = 30, CancellationToken ct = default)
    {
        var userId = GetUserId();
        var startDate = DateTimeOffset.UtcNow.AddDays(-days).Date;

        var transactions = await _context.XpTransactions
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.CreatedAt >= startDate)
            .ToListAsync(ct);

        var grouped = transactions
            .GroupBy(x => x.CreatedAt.Date)
            .Select(g => new
            {
                date = g.Key.ToString("yyyy-MM-dd"),
                xp = g.Sum(x => x.Amount),
                source = g.GroupBy(x => x.Source).Select(s => new { source = s.Key, count = s.Count(), xp = s.Sum(x => x.Amount) }).ToList()
            })
            .OrderBy(x => x.date)
            .ToList();

        return grouped.Cast<object>().ToList();
    }

    [HttpGet("habit-heatmap")]
    public async Task<ActionResult<List<object>>> GetHabitHeatmap([FromQuery] int days = 90, CancellationToken ct = default)
    {
        var userId = GetUserId();
        var startDate = DateTimeOffset.UtcNow.AddDays(-days).Date;

        var completions = await _context.HabitCompletions
            .AsNoTracking()
            .Where(c => c.Habit.UserId == userId && c.CompletedAt >= startDate)
            .Include(c => c.Habit)
            .ToListAsync(ct);

        var grouped = completions
            .GroupBy(c => c.CompletedAt.Date)
            .Select(g => new
            {
                date = g.Key.ToString("yyyy-MM-dd"),
                count = g.Count(),
                habits = g.Select(c => c.Habit.Name).Distinct().ToList()
            })
            .OrderBy(x => x.date)
            .ToList();

        return grouped.Cast<object>().ToList();
    }

    [HttpGet("job-funnel")]
    public async Task<ActionResult<object>> GetJobFunnel(CancellationToken ct)
    {
        var userId = GetUserId();
        var applications = await _context.JobApplications
            .AsNoTracking()
            .Where(a => a.UserId == userId)
            .ToListAsync(ct);

        var statusGroups = applications
            .GroupBy(a => a.Status)
            .ToDictionary(g => g.Key, g => g.Count());

        return new
        {
            saved = statusGroups.GetValueOrDefault("saved", 0),
            applied = statusGroups.GetValueOrDefault("applied", 0),
            phone_screen = statusGroups.GetValueOrDefault("phone_screen", 0),
            interview = statusGroups.GetValueOrDefault("interview", 0),
            offer = statusGroups.GetValueOrDefault("offer", 0),
            rejected = statusGroups.GetValueOrDefault("rejected", 0),
            total = applications.Count
        };
    }

    [HttpGet("coding-streaks")]
    public async Task<ActionResult<object>> GetCodingStreaks(CancellationToken ct)
    {
        var userId = GetUserId();
        var attempts = await _context.ProblemAttempts
            .AsNoTracking()
            .Where(a => a.Problem.UserId == userId)
            .OrderBy(a => a.SolvedAt)
            .ToListAsync(ct);

        var dates = attempts.Select(a => a.SolvedAt.Date).Distinct().OrderBy(d => d).ToList();
        
        int current = 0;
        int longest = 0;
        int temp = 0;
        var today = DateTimeOffset.UtcNow.Date;
        var yesterday = today.AddDays(-1);

        if (dates.Count > 0 && (dates.Last() == today || dates.Last() == yesterday))
        {
            current = 1;
            for (int i = dates.Count - 2; i >= 0; i--)
            {
                if (dates[i] == dates[i + 1].AddDays(-1))
                    current++;
                else
                    break;
            }
        }

        temp = dates.Count > 0 ? 1 : 0;
        for (int i = 1; i < dates.Count; i++)
        {
            if (dates[i] == dates[i - 1].AddDays(1))
            {
                temp++;
                longest = Math.Max(longest, temp);
            }
            else
            {
                temp = 1;
            }
        }
        longest = Math.Max(longest, temp);

        return new
        {
            current,
            longest,
            totalDays = dates.Count,
            totalProblems = attempts.Count
        };
    }

    [HttpGet("overview")]
    public async Task<ActionResult<object>> GetOverview(CancellationToken ct)
    {
        var userId = GetUserId();
        var totalXp = await _context.XpTransactions.Where(x => x.UserId == userId).SumAsync(x => x.Amount, ct);
        var habitsCount = await _context.Habits.CountAsync(h => h.UserId == userId && h.IsActive, ct);
        var jobsCount = await _context.Jobs.CountAsync(j => j.UserId == userId, ct);
        var codingCount = await _context.ProblemAttempts.CountAsync(a => a.Problem.UserId == userId, ct);
        var docsCount = await _context.Documents.CountAsync(d => d.UserId == userId, ct);

        return new
        {
            totalXp,
            habitsCount,
            jobsCount,
            codingCount,
            docsCount
        };
    }
}
