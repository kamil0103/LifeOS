using System.Text.Json;
using LifeOS.Application.Interfaces;
using LifeOS.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AtsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IAiProvider _aiProvider;

    public AtsController(AppDbContext context, IAiProvider aiProvider)
    {
        _context = context;
        _aiProvider = aiProvider;
    }

    private Guid GetUserId()
    {
        var sub = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        return Guid.Parse(sub!);
    }

    [HttpPost("check")]
    public async Task<ActionResult<AtsCheckResult>> CheckResume([FromBody] AtsCheckRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        var job = await _context.Jobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == request.JobId && j.UserId == userId, ct);
        if (job == null)
            return NotFound();

        var resumeData = request.ResumeData ?? await BuildResumeData(userId, ct);

        var systemPrompt = "You are an ATS (Applicant Tracking System) expert. Analyze how well a resume matches a job description and provide actionable feedback. Return JSON only.";
        var userPrompt = $"Job Description:\n{job.Description ?? job.Title}\n\nTitle: {job.Title}\nCompany: {job.Company}\n\nResume:\n{resumeData}\n\nReturn JSON:\n{{\n  \"score\": <number 0-100>,\n  \"keywordMatches\": [{{\"keyword\": \"<word>\", \"found\": true/false}}],\n  \"missingKeywords\": [\"<list>\"],\n  \"formatIssues\": [\"<list>\"],\n  \"suggestions\": [\"<list>\"],\n  \"summary\": \"<assessment>\"\n}}";

        try
        {
            var jsonResponse = await _aiProvider.CompleteJsonAsync(systemPrompt, userPrompt, ct);
            var result = JsonSerializer.Deserialize<AtsCheckResult>(jsonResponse, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return result ?? new AtsCheckResult { Score = 50, Summary = "Could not analyze." };
        }
        catch (Exception ex)
        {
            return BadRequest(new ProblemDetails { Title = "ATS check failed", Detail = ex.Message });
        }
    }

    private async Task<string> BuildResumeData(Guid userId, CancellationToken ct)
    {
        var profile = await _context.UserProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId, ct);
        var skills = await _context.Skills.AsNoTracking().Where(s => s.UserId == userId).Select(s => s.Name).ToListAsync(ct);
        var experience = await _context.WorkExperiences.AsNoTracking().Where(e => e.UserId == userId).ToListAsync(ct);
        var projects = await _context.Projects.AsNoTracking().Where(p => p.UserId == userId).ToListAsync(ct);
        var education = await _context.Degrees.AsNoTracking().Where(d => d.UserId == userId).Include(d => d.Institution).ToListAsync(ct);

        var lines = new List<string>();
        if (profile?.FullName != null) lines.Add($"Name: {profile.FullName}");
        if (profile?.Summary != null) lines.Add($"Summary: {profile.Summary}");
        if (skills.Any()) lines.Add($"Skills: {string.Join(", ", skills)}");
        foreach (var exp in experience)
        {
            lines.Add($"Experience: {exp.Title} at {exp.Company}");
            if (!string.IsNullOrWhiteSpace(exp.Bullets))
                lines.Add(exp.Bullets);
        }
        foreach (var proj in projects)
        {
            lines.Add($"Project: {proj.Name} - {proj.Description}");
        }
        foreach (var edu in education)
        {
            lines.Add($"Education: {edu.DegreeName} at {edu.Institution?.Name}");
        }

        return string.Join("\n", lines);
    }
}

public class AtsCheckRequest
{
    public Guid JobId { get; set; }
    public string? ResumeData { get; set; }
}

public class AtsCheckResult
{
    public int Score { get; set; }
    public List<KeywordMatch> KeywordMatches { get; set; } = new();
    public List<string> MissingKeywords { get; set; } = new();
    public List<string> FormatIssues { get; set; } = new();
    public List<string> Suggestions { get; set; } = new();
    public string Summary { get; set; } = string.Empty;
}

public class KeywordMatch
{
    public string Keyword { get; set; } = string.Empty;
    public bool Found { get; set; }
}
