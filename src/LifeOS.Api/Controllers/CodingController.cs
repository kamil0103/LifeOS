using LifeOS.Application.Interfaces;
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
    private readonly ILocalDayService _days;
    private readonly ICodingProgressService _progress;

    public CodingController(AppDbContext context, ILocalDayService days, ICodingProgressService progress)
    {
        _context = context;
        _days = days;
        _progress = progress;
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
            .Include(p => p.Attempts)
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
    public async Task<ActionResult<CodingProblemDto>> CreateProblem(CreateCodingProblemRequest request, CancellationToken ct)
        => await _progress.CreateAsync(GetUserId(), request, ct);

    [HttpPut("problems/{id}")]
    public async Task<ActionResult<CodingProblemDto>> UpdateProblem(Guid id, UpdateCodingProblemRequest request, CancellationToken ct)
        => await _progress.UpdateAsync(GetUserId(), id, request, ct) is { } result ? Ok(result) : NotFound();

    [HttpDelete("problems/{id}")]
    public async Task<IActionResult> DeleteProblem(Guid id, CancellationToken ct)
        => await _progress.DeleteAsync(GetUserId(), id, ct) ? NoContent() : NotFound();

    [HttpPost("problems/{id}/solve")]
    public async Task<ActionResult<CodingProblemDto>> SolveProblem(Guid id, SolveProblemRequest request, CancellationToken ct)
        => await _progress.RecordAsync(GetUserId(), id, request, false, ct) is { } result ? Ok(result) : NotFound();

    [HttpPost("problems/{id}/reattempt")]
    public async Task<ActionResult<CodingProblemDto>> ReattemptProblem(Guid id, SolveProblemRequest request, CancellationToken ct)
        => await _progress.RecordAsync(GetUserId(), id, request, true, ct) is { } result ? Ok(result) : NotFound();

    [HttpPost("problems/{id}/unsolve")]
    public async Task<ActionResult<CodingProblemDto>> UnsolveProblem(Guid id, CancellationToken ct)
        => await _progress.UnsolveAsync(GetUserId(), id, ct) is { } result ? Ok(result) : NotFound();

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
        var attempts = problems.SelectMany(p => p.Attempts).ToList();

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
        var timestamps = await _context.ProblemAttempts
            .AsNoTracking()
            .Where(a => a.Problem.UserId == userId)
            .Select(a => a.SolvedAt)
            .ToListAsync(ct);
        var localDay = await _days.TodayAsync(userId, ct);
        var attempts = timestamps.Select(localDay.DateOf).Distinct().OrderByDescending(d => d).ToList();

        if (attempts.Count == 0) return 0;

        var day = await _days.TodayAsync(userId, ct);
        var today = day.Date;
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
        var timestamps = await _context.ProblemAttempts
            .AsNoTracking()
            .Where(a => a.Problem.UserId == userId)
            .Select(a => a.SolvedAt)
            .ToListAsync(ct);
        var localDay = await _days.TodayAsync(userId, ct);
        var dates = timestamps.Select(localDay.DateOf).Distinct().OrderBy(d => d).ToList();

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
