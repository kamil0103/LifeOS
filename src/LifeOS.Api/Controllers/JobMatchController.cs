using System.Text.Json;
using LifeOS.Application.DTOs.JobMatch;
using LifeOS.Application.Interfaces;
using LifeOS.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class JobMatchController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IAiProvider _aiProvider;

    public JobMatchController(AppDbContext context, IAiProvider aiProvider)
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

    [HttpPost("analyze")]
    public async Task<ActionResult<JobMatchResultDto>> AnalyzeMatch([FromBody] JobMatchRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        var job = await _context.Jobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == request.JobId && j.UserId == userId, ct);
        if (job == null)
            return NotFound();

        var profile = await _context.UserProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId, ct);
        var skills = await _context.Skills.AsNoTracking().Where(s => s.UserId == userId).Select(s => s.Name).ToListAsync(ct);
        var experience = await _context.WorkExperiences.AsNoTracking().Where(e => e.UserId == userId).ToListAsync(ct);
        var projects = await _context.Projects.AsNoTracking().Where(p => p.UserId == userId).Select(p => p.Name).ToListAsync(ct);

        var systemPrompt = "You are an expert technical recruiter and career coach. Analyze the job description against the candidate's profile and return a JSON response.";
        var userPrompt = $"Job Description:\n{job.Description ?? job.Title}\n\nCompany: {job.Company}\nTitle: {job.Title}\n\nCandidate Profile:\n- Skills: {string.Join(", ", skills)}\n- Experience: {string.Join(", ", experience.Select(e => $"{e.Title} at {e.Company}"))}\n- Projects: {string.Join(", ", projects)}\n- Target Roles: {profile?.TargetRoles ?? "Not specified"}\n\nReturn ONLY a JSON object with these fields:\n{{\n  \"matchScore\": <number 0-100>,\n  \"matchedSkills\": [<list>],\n  \"missingSkills\": [<list>],\n  \"suggestedImprovements\": [<list>],\n  \"summary\": \"<2-3 sentence assessment>\"\n}}";

        try
        {
            var jsonResponse = await _aiProvider.CompleteJsonAsync(systemPrompt, userPrompt, ct);
            var result = JsonSerializer.Deserialize<JobMatchResultDto>(jsonResponse, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return result ?? new JobMatchResultDto { MatchScore = 50, Summary = "Could not analyze match." };
        }
        catch (Exception ex)
        {
            return BadRequest(new ProblemDetails { Title = "AI analysis failed", Detail = ex.Message });
        }
    }

    [HttpPost("interview-qa")]
    public async Task<ActionResult<InterviewQaDto>> GenerateInterviewQa([FromBody] InterviewQaRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        var job = await _context.Jobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == request.JobId && j.UserId == userId, ct);
        if (job == null)
            return NotFound();

        var systemPrompt = "You are a technical interview coach. Generate realistic interview questions and suggested answers based on the job description. Return JSON only.";
        var userPrompt = $"Job: {job.Title} at {job.Company}\nDescription: {job.Description ?? "Not provided"}\n\nGenerate 8-10 interview questions. Return JSON:\n{{\n  \"questions\": [\n    {{\n      \"question\": \"<question text>\",\n      \"category\": \"<technical|behavioral|system_design>\",\n      \"suggestedAnswer\": \"<sample answer>\",\n      \"keyPoints\": \"<what interviewer is looking for>\"\n    }}\n  ],\n  \"roleFocus\": \"<what this role emphasizes>\",\n  \"preparationTips\": \"<2-3 tips>\"\n}}";

        try
        {
            var jsonResponse = await _aiProvider.CompleteJsonAsync(systemPrompt, userPrompt, ct);
            var result = JsonSerializer.Deserialize<InterviewQaDto>(jsonResponse, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return result ?? new InterviewQaDto { PreparationTips = "Could not generate Q&A." };
        }
        catch (Exception ex)
        {
            return BadRequest(new ProblemDetails { Title = "AI generation failed", Detail = ex.Message });
        }
    }
}
