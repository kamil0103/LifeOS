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

    [HttpPost("tailor")]
    public async Task<ActionResult<TailorResumeResult>> TailorResume([FromBody] TailorResumeRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        var job = await _context.Jobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == request.JobId && j.UserId == userId, ct);
        if (job == null)
            return NotFound(new ProblemDetails { Title = "Job not found" });

        if (request.ResumeData == null)
            return BadRequest(new ProblemDetails { Title = "Missing resume data" });

        var resumeJson = JsonSerializer.Serialize(request.ResumeData, new JsonSerializerOptions { WriteIndented = false });

        var systemPrompt = "You are an expert resume writer following Harvard Career Services guidelines. You create tightly TARGETED resumes: you aggressively filter out anything irrelevant to the specific job, and rewrite what remains using ONLY the candidate's real data — never invent experience, skills, degrees, or achievements.";

        var userPrompt = $@"Tailor this resume to the job below. Your #1 job is FILTERING — a short targeted resume beats a long unfocused one.

FILTERING RULES (most important):
- EXPERIENCE: Remove any role with no transferable relevance to this job (e.g., remove food delivery, retail, or unrelated labor roles from a software engineering resume). If a role has transferable skills (teamwork, deadlines, customer service, reliability), keep it but reframe bullets toward what this job values. If ALL experience is unrelated, keep only the single strongest entry.
- PROJECTS: Keep only projects that demonstrate skills this job asks for. Remove the rest.
- SKILLS: Keep only skills a recruiter for THIS role would care about; most job-relevant first in each category. Remove clearly unrelated skills (e.g., remove cooking or driving skills from a programming resume).
- COURSEWORK: Keep only courses relevant to this job.
- EDUCATION: Keep all degrees, but drop honors/coursework that don't serve this application.

WRITING RULES (Harvard Career Services):
- Begin every bullet with a strong action verb (Developed, Implemented, Analyzed, Engineered, Designed, Led, Optimized, Streamlined).
- Quantify results where the data supports it. Concise phrases, NOT full sentences. No personal pronouns (no I, we, my).
- Rewrite the summary to target this specific role.
- Do NOT fabricate: no new employers, titles, skills, degrees, certifications, or metrics not supported by the original data.
- Keep the same JSON structure exactly. Keep entity IDs unchanged.

JOB POSTING:
Title: {job.Title}
Company: {job.Company}
Description: {job.Description ?? "(no description)"}

CURRENT RESUME JSON:
{resumeJson}

Return ONLY valid JSON with this exact structure:
{{
  ""tailoredResume"": ""<the full FILTERED resume JSON, same structure as the input>"",
  ""changeSummary"": [""<each entry you REMOVED and why, then each thing you rewrote/reordered>""],
  ""recommendations"": [{{ ""type"": ""skill"", ""text"": ""<suggestion for something the job wants that the candidate lacks data for>"", ""target"": ""<optional category>"" }}]
}}
For recommendations, type may be skill, keyword, or note. The changeSummary MUST list every removed experience, project, skill, and course so the user can see exactly what was filtered out.";

        try
        {
            var jsonResponse = await _aiProvider.CompleteJsonAsync(systemPrompt, userPrompt, ct);
            var cleaned = jsonResponse.Trim();
            if (cleaned.StartsWith("```json")) cleaned = cleaned[7..];
            else if (cleaned.StartsWith("```")) cleaned = cleaned[3..];
            if (cleaned.EndsWith("```")) cleaned = cleaned[..^3];
            var firstBrace = cleaned.IndexOf('{');
            var lastBrace = cleaned.LastIndexOf('}');
            if (firstBrace >= 0 && lastBrace > firstBrace)
                cleaned = cleaned[firstBrace..(lastBrace + 1)];

            var result = JsonSerializer.Deserialize<TailorResumeResult>(cleaned, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (result?.TailoredResume == null)
                return BadRequest(new ProblemDetails { Title = "AI returned no resume", Detail = "The AI response could not be parsed." });

            // Normalize nulls so downstream PDF generation never crashes
            var r = result.TailoredResume;
            r.Profile ??= new();
            r.Experience ??= new();
            r.Education ??= new();
            r.Skills ??= new();
            r.Projects ??= new();
            r.Certifications ??= new();
            r.Courses ??= new();
            r.SectionOrder ??= new();
            if (string.IsNullOrWhiteSpace(r.Template)) r.Template = "harvard";
            r.PendingRecommendations = new(); // recommendations live in result.Recommendations only

            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new ProblemDetails { Title = "Tailoring failed", Detail = ex.Message });
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

public class TailorResumeRequest
{
    public Guid JobId { get; set; }
    public LifeOS.Application.DTOs.Documents.ResumeDataDto? ResumeData { get; set; }
    public string Mode { get; set; } = "modify"; // "modify" | "create"
}

public class TailorResumeResult
{
    public LifeOS.Application.DTOs.Documents.ResumeDataDto TailoredResume { get; set; } = new();
    public List<string> ChangeSummary { get; set; } = new();
    public List<LifeOS.Application.DTOs.Documents.ResumeRecommendationDto> Recommendations { get; set; } = new();
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
