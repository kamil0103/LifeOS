using LifeOS.Application.DTOs.Coding;
using LifeOS.Domain.Entities;
using LifeOS.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CodingController : ControllerBase
{
    private readonly AppDbContext _context;

    public CodingController(AppDbContext context)
    {
        _context = context;
    }

    private Guid GetUserId()
    {
        var sub = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        return Guid.Parse(sub!);
    }

    // ==================== PROBLEMS CRUD ====================

    [HttpGet("problems")]
    public async Task<ActionResult<List<CodingProblemDto>>> GetProblems(
        [FromQuery] string? difficulty,
        [FromQuery] string? category,
        [FromQuery] bool? solved,
        CancellationToken ct)
    {
        var userId = GetUserId();
        var query = _context.CodingProblems
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(difficulty))
            query = query.Where(p => p.Difficulty == difficulty.ToLower());
        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(p => p.Category != null && p.Category.ToLower() == category.ToLower());
        if (solved.HasValue)
            query = query.Where(p => p.IsSolved == solved.Value);

        var problems = await query
            .OrderByDescending(p => p.IsSolved)
            .ThenByDescending(p => p.SolvedAt)
            .ThenBy(p => p.Title)
            .ToListAsync(ct);

        return problems.Select(MapToDto).ToList();
    }

    [HttpPost("problems")]
    public async Task<ActionResult<CodingProblemDto>> CreateProblem([FromBody] CreateCodingProblemRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        var problem = new CodingProblem
        {
            UserId = userId,
            Title = request.Title,
            Platform = request.Platform,
            Url = request.Url,
            Difficulty = request.Difficulty.ToLower(),
            Category = request.Category,
            Description = request.Description,
            Notes = request.Notes
        };

        _context.CodingProblems.Add(problem);
        await _context.SaveChangesAsync(ct);

        return MapToDto(problem);
    }

    [HttpPut("problems/{id}")]
    public async Task<ActionResult<CodingProblemDto>> UpdateProblem(Guid id, [FromBody] UpdateCodingProblemRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        var problem = await _context.CodingProblems.FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId, ct);
        if (problem == null)
            return NotFound();

        problem.Title = request.Title;
        problem.Platform = request.Platform;
        problem.Url = request.Url;
        problem.Difficulty = request.Difficulty.ToLower();
        problem.Category = request.Category;
        problem.Description = request.Description;
        problem.Notes = request.Notes;

        await _context.SaveChangesAsync(ct);
        return MapToDto(problem);
    }

    [HttpDelete("problems/{id}")]
    public async Task<IActionResult> DeleteProblem(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        var problem = await _context.CodingProblems
            .Include(p => p.Attempts)
            .FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId, ct);
        if (problem == null)
            return NotFound();

        _context.ProblemAttempts.RemoveRange(problem.Attempts);
        _context.CodingProblems.Remove(problem);
        await _context.SaveChangesAsync(ct);
        return NoContent();
    }

    // ==================== SOLVE ====================

    [HttpPost("problems/{id}/solve")]
    public async Task<ActionResult<CodingProblemDto>> SolveProblem(Guid id, [FromBody] SolveProblemRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        var problem = await _context.CodingProblems
            .Include(p => p.Attempts)
            .FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId, ct);
        if (problem == null)
            return NotFound();

        // Calculate XP based on difficulty
        var xpEarned = problem.Difficulty.ToLower() switch
        {
            "easy" => 10,
            "medium" => 25,
            "hard" => 50,
            _ => 10
        };

        // First solve of the day bonus
        var today = DateTimeOffset.UtcNow.Date;
        var solvedToday = await _context.ProblemAttempts
            .AnyAsync(a => a.Problem.UserId == userId && a.SolvedAt.Date == today, ct);
        if (!solvedToday)
            xpEarned += 5;

        // Streak bonus
        var streak = await GetCurrentStreak(userId, ct);
        if (streak > 0)
            xpEarned += Math.Min(streak * 2, 20); // Cap at +20

        var attempt = new ProblemAttempt
        {
            ProblemId = id,
            SolvedAt = DateTimeOffset.UtcNow,
            SolutionLanguage = request.SolutionLanguage,
            TimeSpentMinutes = request.TimeSpentMinutes,
            Notes = request.Notes,
            XpEarned = xpEarned
        };

        _context.ProblemAttempts.Add(attempt);

        problem.IsSolved = true;
        problem.SolvedAt = DateTimeOffset.UtcNow;
        problem.SolutionLanguage = request.SolutionLanguage;
        problem.TimeSpentMinutes = request.TimeSpentMinutes;

        // Award XP
        _context.XpTransactions.Add(new XpTransaction
        {
            UserId = userId,
            Amount = xpEarned,
            Source = "coding",
            Description = $"Solved '{problem.Title}' ({problem.Difficulty})",
            CreatedAt = DateTimeOffset.UtcNow
        });

        await _context.SaveChangesAsync(ct);
        return MapToDto(problem);
    }

    [HttpPost("problems/{id}/unsolve")]
    public async Task<ActionResult<CodingProblemDto>> UnsolveProblem(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        var problem = await _context.CodingProblems
            .Include(p => p.Attempts)
            .FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId, ct);
        if (problem == null)
            return NotFound();

        problem.IsSolved = false;
        problem.SolvedAt = null;
        problem.SolutionLanguage = null;
        problem.TimeSpentMinutes = null;

        await _context.SaveChangesAsync(ct);
        return MapToDto(problem);
    }

    [HttpPost("problems/{id}/reattempt")]
    public async Task<ActionResult<CodingProblemDto>> ReattemptProblem(Guid id, [FromBody] SolveProblemRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        var problem = await _context.CodingProblems
            .Include(p => p.Attempts)
            .FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId, ct);
        if (problem == null)
            return NotFound();

        var xpEarned = 5; // Reattempt bonus

        var attempt = new ProblemAttempt
        {
            ProblemId = id,
            SolvedAt = DateTimeOffset.UtcNow,
            SolutionLanguage = request.SolutionLanguage,
            TimeSpentMinutes = request.TimeSpentMinutes,
            Notes = request.Notes,
            XpEarned = xpEarned
        };

        _context.ProblemAttempts.Add(attempt);

        problem.SolvedAt = DateTimeOffset.UtcNow;
        problem.SolutionLanguage = request.SolutionLanguage;
        problem.TimeSpentMinutes = request.TimeSpentMinutes;

        _context.XpTransactions.Add(new XpTransaction
        {
            UserId = userId,
            Amount = xpEarned,
            Source = "coding",
            Description = $"Re-solved '{problem.Title}'",
            CreatedAt = DateTimeOffset.UtcNow
        });

        await _context.SaveChangesAsync(ct);
        return MapToDto(problem);
    }

    // ==================== ATTEMPTS ====================

    [HttpGet("problems/{id}/attempts")]
    public async Task<ActionResult<List<ProblemAttemptDto>>> GetAttempts(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        var problem = await _context.CodingProblems.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId, ct);
        if (problem == null)
            return NotFound();

        var attempts = await _context.ProblemAttempts
            .AsNoTracking()
            .Where(a => a.ProblemId == id)
            .OrderByDescending(a => a.SolvedAt)
            .ToListAsync(ct);

        return attempts.Select(a => new ProblemAttemptDto
        {
            Id = a.Id,
            ProblemId = a.ProblemId,
            SolvedAt = a.SolvedAt,
            SolutionLanguage = a.SolutionLanguage,
            TimeSpentMinutes = a.TimeSpentMinutes,
            Notes = a.Notes,
            XpEarned = a.XpEarned
        }).ToList();
    }

    // ==================== STATS ====================

    [HttpGet("stats")]
    public async Task<ActionResult<CodingStatsDto>> GetStats(CancellationToken ct)
    {
        var userId = GetUserId();
        var problems = await _context.CodingProblems
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .Include(p => p.Attempts)
            .ToListAsync(ct);

        var solved = problems.Where(p => p.IsSolved).ToList();
        var attempts = solved.SelectMany(p => p.Attempts).ToList();

        var byCategory = solved
            .Where(p => !string.IsNullOrWhiteSpace(p.Category))
            .GroupBy(p => p.Category!)
            .Select(g => new CategoryStatDto { Category = g.Key, SolvedCount = g.Count() })
            .OrderByDescending(c => c.SolvedCount)
            .ToList();

        var byLanguage = attempts
            .Where(a => !string.IsNullOrWhiteSpace(a.SolutionLanguage))
            .GroupBy(a => a.SolutionLanguage!)
            .Select(g => new LanguageStatDto { Language = g.Key, SolvedCount = g.Count() })
            .OrderByDescending(l => l.SolvedCount)
            .ToList();

        return new CodingStatsDto
        {
            TotalProblems = problems.Count,
            SolvedProblems = solved.Count,
            EasySolved = solved.Count(p => p.Difficulty == "easy"),
            MediumSolved = solved.Count(p => p.Difficulty == "medium"),
            HardSolved = solved.Count(p => p.Difficulty == "hard"),
            CurrentStreak = await GetCurrentStreak(userId, ct),
            LongestStreak = await GetLongestStreak(userId, ct),
            TotalXpEarned = attempts.Sum(a => a.XpEarned),
            ByCategory = byCategory,
            ByLanguage = byLanguage
        };
    }

    [HttpGet("streak")]
    public async Task<ActionResult<object>> GetStreak(CancellationToken ct)
    {
        var userId = GetUserId();
        return new
        {
            current = await GetCurrentStreak(userId, ct),
            longest = await GetLongestStreak(userId, ct)
        };
    }

    // ==================== HELPERS ====================

    private static CodingProblemDto MapToDto(CodingProblem p)
    {
        return new CodingProblemDto
        {
            Id = p.Id,
            Title = p.Title,
            Platform = p.Platform,
            Url = p.Url,
            Difficulty = p.Difficulty,
            Category = p.Category,
            Description = p.Description,
            Notes = p.Notes,
            IsSolved = p.IsSolved,
            SolvedAt = p.SolvedAt,
            SolutionLanguage = p.SolutionLanguage,
            TimeSpentMinutes = p.TimeSpentMinutes,
            AttemptCount = p.Attempts?.Count ?? 0
        };
    }

    private async Task<int> GetCurrentStreak(Guid userId, CancellationToken ct)
    {
        var attempts = await _context.ProblemAttempts
            .AsNoTracking()
            .Where(a => a.Problem.UserId == userId)
            .Select(a => a.SolvedAt.Date)
            .Distinct()
            .OrderByDescending(d => d)
            .ToListAsync(ct);

        if (attempts.Count == 0) return 0;

        var today = DateTimeOffset.UtcNow.Date;
        var yesterday = today.AddDays(-1);

        // Check if solved today or yesterday
        if (attempts[0] != today && attempts[0] != yesterday)
            return 0;

        int streak = 1;
        for (int i = 1; i < attempts.Count; i++)
        {
            if (attempts[i] == attempts[i - 1].AddDays(-1))
                streak++;
            else
                break;
        }
        return streak;
    }

    private async Task<int> GetLongestStreak(Guid userId, CancellationToken ct)
    {
        var dates = await _context.ProblemAttempts
            .AsNoTracking()
            .Where(a => a.Problem.UserId == userId)
            .Select(a => a.SolvedAt.Date)
            .Distinct()
            .OrderBy(d => d)
            .ToListAsync(ct);

        if (dates.Count == 0) return 0;

        int longest = 1;
        int current = 1;
        for (int i = 1; i < dates.Count; i++)
        {
            if (dates[i] == dates[i - 1].AddDays(1))
            {
                current++;
                longest = Math.Max(longest, current);
            }
            else
            {
                current = 1;
            }
        }
        return longest;
    }
}
