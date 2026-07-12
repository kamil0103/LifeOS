using System.Text.Json;
using LifeOS.Application.Interfaces;
using LifeOS.Domain.Entities;
using LifeOS.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AiCoachController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IAiProvider _aiProvider;
    private readonly ILogger<AiCoachController> _logger;

    public AiCoachController(AppDbContext context, IAiProvider aiProvider, ILogger<AiCoachController> logger)
    {
        _context = context;
        _aiProvider = aiProvider;
        _logger = logger;
    }

    private Guid GetUserId()
    {
        var sub = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        return Guid.Parse(sub!);
    }

    [HttpPost("generate-mission")]
    public async Task<ActionResult<DailyMissionDto>> GenerateMission(CancellationToken ct)
    {
        var userId = GetUserId();
        var today = DateTimeOffset.UtcNow.Date;

        // Check if already generated today
        var existing = await _context.DailyMissions
            .FirstOrDefaultAsync(m => m.UserId == userId && m.MissionDate == today, ct);

        if (existing != null)
        {
            return Ok(new DailyMissionDto
            {
                Id = existing.Id,
                Date = existing.MissionDate,
                Priorities = JsonSerializer.Deserialize<List<MissionPriority>>(existing.PrioritiesJson) ?? [],
                AiSummary = existing.AiSummary,
                IsCompleted = existing.CompletedAt.HasValue
            });
        }

        // Gather user data for context
        var habits = await _context.Habits
            .AsNoTracking()
            .Where(h => h.UserId == userId && h.IsActive)
            .Include(h => h.Streak)
            .Include(h => h.Completions.Where(c => c.CompletedAt >= today.AddDays(-7)))
            .ToListAsync(ct);

        var jobStats = await _context.Jobs
            .AsNoTracking()
            .Where(j => j.UserId == userId)
            .GroupBy(j => 1)
            .Select(g => new {
                TotalSaved = g.Count(j => j.Status == "saved"),
                TotalApplied = g.Count(j => j.Status == "applied"),
                LastApplicationDate = g.Where(j => j.Status == "applied").OrderByDescending(j => j.CreatedAt).Select(j => (DateTimeOffset?)j.CreatedAt).FirstOrDefault()
            })
            .FirstOrDefaultAsync(ct);

        var totalXp = await _context.XpTransactions
            .Where(x => x.UserId == userId)
            .SumAsync(x => x.Amount, ct);

        var profile = await _context.UserProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId, ct);

        var skills = await _context.Skills
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .Select(s => new { s.Name, s.Category, s.Proficiency })
            .ToListAsync(ct);

        // Build AI prompt
        var systemPrompt = "You are LifeOS AI Coach, a disciplined and encouraging mentor helping a software engineer grow professionally and spiritually. You analyze their data and generate actionable daily missions.";

        var userPrompt = $"User Profile:\n" +
            $"- Name: {profile?.FullName ?? "User"}\n" +
            $"- Target Roles: {profile?.TargetRoles ?? "Not set"}\n" +
            $"- Total XP: {totalXp}\n" +
            $"- Skills: {string.Join(", ", skills.Select(s => s.Name))}\n\n" +
            $"Current Status:\n" +
            $"- Active Habits: {habits.Count}\n" +
            $"- Job Applications This Period: {jobStats?.TotalApplied ?? 0}\n" +
            $"- Saved Jobs: {jobStats?.TotalSaved ?? 0}\n\n" +
            $"Habit Streaks:\n{string.Join("\n", habits.Select(h => $"- {h.Name}: {h.Streak?.CurrentStreak ?? 0} day streak"))}\n\n" +
            "Generate a JSON response with today's mission:\n" +
            "{\n" +
            "  \"priorities\": [\n" +
            "    { \"title\": \"<action item>\", \"category\": \"<coding|jobs|habits|spiritual>\", \"priority\": \"<high|medium|low>\", \"reason\": \"<why this matters>\" }\n" +
            "  ],\n" +
            "  \"summary\": \"<2-3 sentence motivational summary>\",\n" +
            "  \"warning\": \"<warning about gaps if any, or null>\",\n" +
            "  \"encouragement\": \"<encouraging observation about progress>\"\n" +
            "}";

        try
        {
            var jsonResponse = await _aiProvider.CompleteJsonAsync(systemPrompt, userPrompt, ct);
            var missionData = JsonSerializer.Deserialize<AiMissionResponse>(jsonResponse, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (missionData == null)
                return BadRequest("Failed to parse AI mission response");

            // Save mission
            var mission = new DailyMission
            {
                UserId = userId,
                MissionDate = today,
                PrioritiesJson = JsonSerializer.Serialize(missionData.Priorities),
                AiSummary = missionData.Summary
            };
            _context.DailyMissions.Add(mission);

            // Save warning message if present
            if (!string.IsNullOrEmpty(missionData.Warning))
            {
                _context.AiCoachMessages.Add(new AiCoachMessage
                {
                    UserId = userId,
                    MessageType = "warning",
                    Content = missionData.Warning,
                    ContextJson = JsonSerializer.Serialize(new { source = "mission_generation" })
                });
            }

            // Save encouragement message
            if (!string.IsNullOrEmpty(missionData.Encouragement))
            {
                _context.AiCoachMessages.Add(new AiCoachMessage
                {
                    UserId = userId,
                    MessageType = "encouragement",
                    Content = missionData.Encouragement,
                    ContextJson = JsonSerializer.Serialize(new { source = "mission_generation" })
                });
            }

            await _context.SaveChangesAsync(ct);

            return Ok(new DailyMissionDto
            {
                Id = mission.Id,
                Date = mission.MissionDate,
                Priorities = missionData.Priorities,
                AiSummary = missionData.Summary,
                IsCompleted = false
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate AI mission");
            return BadRequest($"AI generation failed: {ex.Message}");
        }
    }

    [HttpGet("mission")]
    public async Task<ActionResult<DailyMissionDto>> GetTodayMission(CancellationToken ct)
    {
        var userId = GetUserId();
        var today = DateTimeOffset.UtcNow.Date;

        var mission = await _context.DailyMissions
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.UserId == userId && m.MissionDate == today, ct);

        if (mission == null)
            return NotFound("No mission generated yet. Call POST /api/aicoach/generate-mission first.");

        return Ok(new DailyMissionDto
        {
            Id = mission.Id,
            Date = mission.MissionDate,
            Priorities = JsonSerializer.Deserialize<List<MissionPriority>>(mission.PrioritiesJson) ?? [],
            AiSummary = mission.AiSummary,
            IsCompleted = mission.CompletedAt.HasValue
        });
    }

    [HttpPost("mission/{id:guid}/complete")]
    public async Task<ActionResult<DailyMissionDto>> CompleteMission(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        var mission = await _context.DailyMissions
            .FirstOrDefaultAsync(m => m.Id == id && m.UserId == userId, ct);

        if (mission == null) return NotFound();

        mission.CompletedAt = DateTimeOffset.UtcNow;
        await _context.SaveChangesAsync(ct);

        return Ok(new DailyMissionDto
        {
            Id = mission.Id,
            Date = mission.MissionDate,
            Priorities = JsonSerializer.Deserialize<List<MissionPriority>>(mission.PrioritiesJson) ?? [],
            AiSummary = mission.AiSummary,
            IsCompleted = true
        });
    }

    [HttpGet("messages")]
    public async Task<ActionResult<List<AiCoachMessageDto>>> GetMessages([FromQuery] int limit = 10, CancellationToken ct = default)
    {
        var userId = GetUserId();
        var messages = await _context.AiCoachMessages
            .AsNoTracking()
            .Where(m => m.UserId == userId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);

        return Ok(messages.Select(m => new AiCoachMessageDto
        {
            Id = m.Id,
            MessageType = m.MessageType,
            Content = m.Content,
            IsRead = m.IsRead,
            CreatedAt = m.CreatedAt
        }).ToList());
    }

    [HttpPost("messages/{id:guid}/read")]
    public async Task<IActionResult> MarkMessageRead(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        var message = await _context.AiCoachMessages
            .FirstOrDefaultAsync(m => m.Id == id && m.UserId == userId, ct);

        if (message == null) return NotFound();

        message.IsRead = true;
        await _context.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("chat")]
    public async Task<ActionResult<string>> Chat([FromBody] ChatRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        
        var systemPrompt = "You are LifeOS AI Coach, a wise and encouraging mentor for a software engineer. Be concise, actionable, and supportive. You understand their goal is to transition from delivery work to software engineering.";

        try
        {
            var response = await _aiProvider.CompleteAsync(systemPrompt, request.Message, ct);
            return Ok(new { response });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI chat failed");
            return BadRequest($"AI chat failed: {ex.Message}");
        }
    }
}

public class DailyMissionDto
{
    public Guid Id { get; set; }
    public DateTime Date { get; set; }
    public List<MissionPriority> Priorities { get; set; } = [];
    public string? AiSummary { get; set; }
    public bool IsCompleted { get; set; }
}

public class MissionPriority
{
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}

public class AiMissionResponse
{
    public List<MissionPriority> Priorities { get; set; } = [];
    public string Summary { get; set; } = string.Empty;
    public string? Warning { get; set; }
    public string? Encouragement { get; set; }
}

public class AiCoachMessageDto
{
    public Guid Id { get; set; }
    public string MessageType { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class ChatRequest
{
    public string Message { get; set; } = string.Empty;
}
