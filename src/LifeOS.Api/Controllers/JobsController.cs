using System.Text.Json;
using LifeOS.Application.DTOs.Jobs;
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
public class JobsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IAiProvider _aiProvider;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;

    public JobsController(AppDbContext context, IAiProvider aiProvider, IHttpClientFactory httpClientFactory, IConfiguration config)
    {
        _context = context;
        _aiProvider = aiProvider;
        _httpClientFactory = httpClientFactory;
        _config = config;
    }

    private Guid GetUserId()
    {
        var sub = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        return Guid.Parse(sub!);
    }

    // ==================== CRUD ====================
    [HttpGet]
    public async Task<ActionResult<List<JobDto>>> GetJobs(
        [FromQuery] string? status,
        [FromQuery] string? search,
        CancellationToken ct)
    {
        var userId = GetUserId();
        var query = _context.Jobs.AsNoTracking().Where(j => j.UserId == userId);

        if (!string.IsNullOrEmpty(status))
            query = query.Where(j => j.Status == status);

        if (!string.IsNullOrEmpty(search))
            query = query.Where(j => 
                EF.Functions.ILike(j.Title, $"%{search}%") ||
                EF.Functions.ILike(j.Company, $"%{search}%"));

        var jobs = await query.OrderByDescending(j => j.CreatedAt).ToListAsync(ct);
        return Ok(jobs.Select(MapJob));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<JobDto>> GetJob(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        var job = await _context.Jobs
            .AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == id && j.UserId == userId, ct);

        if (job == null) return NotFound();
        return Ok(MapJob(job));
    }

    [HttpPost]
    public async Task<ActionResult<JobDto>> CreateJob(CreateJobRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        var job = new Job
        {
            UserId = userId,
            Title = request.Title,
            Company = request.Company,
            Location = request.Location,
            Description = request.Description,
            Url = request.Url,
            Source = request.Source,
            SalaryRange = request.SalaryRange,
            JobType = request.JobType,
            PostedDate = request.PostedDate,
            Status = "saved"
        };

        _context.Jobs.Add(job);
        await _context.SaveChangesAsync(ct);
        return Ok(MapJob(job));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<JobDto>> UpdateJob(Guid id, UpdateJobRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        var job = await _context.Jobs
            .FirstOrDefaultAsync(j => j.Id == id && j.UserId == userId, ct);

        if (job == null) return NotFound();

        job.Title = request.Title;
        job.Company = request.Company;
        job.Location = request.Location;
        job.Description = request.Description;
        job.Url = request.Url;
        job.SalaryRange = request.SalaryRange;
        job.JobType = request.JobType;
        job.PostedDate = request.PostedDate;
        job.MatchScore = request.MatchScore;
        job.Status = request.Status;

        await _context.SaveChangesAsync(ct);
        return Ok(MapJob(job));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteJob(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        var job = await _context.Jobs
            .FirstOrDefaultAsync(j => j.Id == id && j.UserId == userId, ct);

        if (job == null) return NotFound();

        _context.Jobs.Remove(job);
        await _context.SaveChangesAsync(ct);
        return NoContent();
    }

    // ==================== AI MATCH SCORE ====================
    [HttpPost("{id:guid}/analyze")]
    public async Task<ActionResult<JobMatchResult>> AnalyzeJob(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        var job = await _context.Jobs
            .AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == id && j.UserId == userId, ct);

        if (job == null) return NotFound();

        var skills = await _context.Skills
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .Select(s => s.Name)
            .ToListAsync(ct);

        var experiences = await _context.WorkExperiences
            .AsNoTracking()
            .Where(w => w.UserId == userId)
            .Select(w => w.Title + " at " + w.Company)
            .ToListAsync(ct);

        var systemPrompt = "You are an expert resume matcher. Analyze the fit between a candidate and a job.";
        var userPrompt = $@"Candidate Skills: {string.Join(", ", skills)}
Candidate Experience: {string.Join("; ", experiences)}

Job Description:
{job.Description}

Provide a JSON response with this exact structure:
{{
  ""matchScore"": <number 0-100>,
  ""analysis"": ""<brief analysis text>"",
  ""matchedSkills"": [""<skill1>"", ""<skill2>""],
  ""missingSkills"": [""<skill1>"", ""<skill2>""]
}}";

        try
        {
            var jsonResponse = await _aiProvider.CompleteJsonAsync(systemPrompt, userPrompt, ct);
            var result = JsonSerializer.Deserialize<JobMatchResult>(jsonResponse, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            
            if (result == null)
                return BadRequest("Failed to parse AI response");

            // Update job with match score
            var jobToUpdate = await _context.Jobs.FindAsync(new object[] { id }, ct);
            if (jobToUpdate != null)
            {
                jobToUpdate.MatchScore = result.MatchScore;
                await _context.SaveChangesAsync(ct);
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest($"AI analysis failed: {ex.Message}");
        }
    }

    // ==================== DISCOVERY ====================
    [HttpGet("discover")]
    public async Task<ActionResult<List<ExternalJobDto>>> DiscoverJobs(
        [FromQuery] string? keywords,
        [FromQuery] string? location,
        CancellationToken ct)
    {
        var results = new List<ExternalJobDto>();

        // The Muse API
        try
        {
            var museKey = _config["MuseApiKey"];
            if (!string.IsNullOrEmpty(museKey))
            {
                var client = _httpClientFactory.CreateClient();
                var url = $"https://www.themuse.com/api/public/jobs?api_key={museKey}&page=1&descending=true";
                if (!string.IsNullOrEmpty(keywords)) url += $"&category={Uri.EscapeDataString(keywords)}";
                if (!string.IsNullOrEmpty(location)) url += $"&location={Uri.EscapeDataString(location)}";

                var response = await client.GetAsync(url, ct);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync(ct);
                    using var doc = JsonDocument.Parse(json);
                    var jobs = doc.RootElement.GetProperty("results");
                    foreach (var job in jobs.EnumerateArray().Take(10))
                    {
                        results.Add(new ExternalJobDto
                        {
                            Title = job.GetProperty("name").GetString() ?? "",
                            Company = job.GetProperty("company").GetProperty("name").GetString() ?? "",
                            Location = job.TryGetProperty("locations", out var locs) && locs.GetArrayLength() > 0
                                ? locs[0].GetProperty("name").GetString() : null,
                            Description = job.GetProperty("contents").GetString() ?? "",
                            Url = job.GetProperty("refs").GetProperty("landing_page").GetString(),
                            Source = "muse",
                            PostedDate = job.TryGetProperty("publication_date", out var pubDate)
                                ? DateTime.TryParse(pubDate.GetString(), out var dt) ? dt : null : null
                        });
                    }
                }
            }
        }
        catch { /* Silently fail external API */ }

        // Arbeitnow RSS
        try
        {
            var client = _httpClientFactory.CreateClient();
            var rssUrl = "https://arbeitnow.com/api/job-board-api";
            var response = await client.GetAsync(rssUrl, ct);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("data", out var data))
                {
                    foreach (var job in data.EnumerateArray().Take(10))
                    {
                        var title = job.GetProperty("title").GetString() ?? "";
                        if (!string.IsNullOrEmpty(keywords) && !title.Contains(keywords, StringComparison.OrdinalIgnoreCase))
                            continue;

                        results.Add(new ExternalJobDto
                        {
                            Title = title,
                            Company = job.GetProperty("company_name").GetString() ?? "",
                            Location = job.TryGetProperty("location", out var loc) ? loc.GetString() : null,
                            Description = job.TryGetProperty("description", out var desc) ? (desc.GetString() ?? "") : "",
                            Url = job.TryGetProperty("url", out var url) ? url.GetString() : null,
                            Source = "arbeitnow",
                            JobType = job.TryGetProperty("job_types", out var jt) && jt.GetArrayLength() > 0
                                ? jt[0].GetString() : null,
                            SalaryRange = job.TryGetProperty("salary", out var sal) ? sal.GetString() : null
                        });
                    }
                }
            }
        }
        catch { /* Silently fail external API */ }

        return Ok(results.Take(20).ToList());
    }

    // ==================== STATS ====================
    [HttpGet("stats")]
    public async Task<ActionResult<ApplicationStatsDto>> GetStats(CancellationToken ct)
    {
        var userId = GetUserId();
        var jobs = await _context.Jobs
            .AsNoTracking()
            .Where(j => j.UserId == userId)
            .ToListAsync(ct);

        return Ok(new ApplicationStatsDto
        {
            TotalSaved = jobs.Count(j => j.Status == "saved"),
            TotalApplied = jobs.Count(j => j.Status == "applied"),
            TotalPhoneScreen = jobs.Count(j => j.Status == "phone_screen"),
            TotalInterview = jobs.Count(j => j.Status == "interview"),
            TotalOffer = jobs.Count(j => j.Status == "offer"),
            TotalRejected = jobs.Count(j => j.Status == "rejected")
        });
    }

    private static JobDto MapJob(Job j) => new()
    {
        Id = j.Id,
        Title = j.Title,
        Company = j.Company,
        Location = j.Location,
        Description = j.Description,
        Url = j.Url,
        Source = j.Source,
        SalaryRange = j.SalaryRange,
        JobType = j.JobType,
        PostedDate = j.PostedDate,
        MatchScore = j.MatchScore,
        Status = j.Status,
        CreatedAt = j.CreatedAt
    };
}
