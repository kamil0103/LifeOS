using System.Net.Mime;
using System.Text.Json;
using LifeOS.Application.DTOs.Documents;
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
public class DocumentsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IResumeGenerator _resumeGenerator;
    private readonly IResumeDataBuilder _resumeDataBuilder;
    private readonly IDocumentStorage _documentStorage;
    private readonly IAiProvider _aiProvider;

    public DocumentsController(
        AppDbContext context,
        ICurrentUserService currentUser,
        IResumeGenerator resumeGenerator,
        IResumeDataBuilder resumeDataBuilder,
        IDocumentStorage documentStorage,
        IAiProvider aiProvider)
    {
        _context = context;
        _currentUser = currentUser;
        _resumeGenerator = resumeGenerator;
        _resumeDataBuilder = resumeDataBuilder;
        _documentStorage = documentStorage;
        _aiProvider = aiProvider;
    }

    private Guid GetUserId()
    {
        return _currentUser.UserId ?? throw new InvalidOperationException("User not authenticated");
    }

    [HttpPost("resume")]
    public async Task<IActionResult> GenerateResume([FromBody] GenerateResumeRequest request, CancellationToken ct)
    {
        var userId = GetUserId();

        // Use the editor's current data when provided; otherwise build fresh from DB
        ResumeDataDto data = request.Data ?? await _resumeDataBuilder.BuildAsync(userId, ct);
        data.Template = request.Template ?? data.Template;
        data.Title = request.Title ?? data.Title;
        if (request.SectionOrder != null && request.SectionOrder.Count > 0)
            data.SectionOrder = request.SectionOrder;

        // Normalize nulls so QuestPDF never throws on missing data
        data.Profile ??= new ResumeProfileDto();
        data.Experience ??= new();
        data.Education ??= new();
        data.Skills ??= new();
        data.Projects ??= new();
        data.Certifications ??= new();
        data.Courses ??= new();

        if (string.IsNullOrWhiteSpace(data.Template))
            data.Template = "harvard";
        if (data.SectionOrder == null || data.SectionOrder.Count == 0)
            data.SectionOrder = new List<string> { "education", "experience", "skills", "projects", "certifications" };

        var pdfBytes = await _resumeGenerator.GenerateResumePdfAsync(data, data.Template, ct);
        var filename = $"{SafeFilename(data.Profile.FullName)}_resume_{data.Template}_{DateTimeOffset.UtcNow:yyyyMMdd_HHmmss}.pdf";
        var storagePath = await _documentStorage.SaveAsync(pdfBytes, filename, userId.ToString(), ct);

        var doc = new Document
        {
            UserId = userId,
            Type = "resume",
            Filename = filename,
            StoragePath = storagePath,
            GeneratedAt = DateTimeOffset.UtcNow
        };
        _context.Documents.Add(doc);
        await _context.SaveChangesAsync(ct);

        return File(pdfBytes, MediaTypeNames.Application.Pdf, filename);
    }

    [HttpPost("cover-letter")]
    public async Task<IActionResult> GenerateCoverLetter([FromBody] CoverLetterDataDto data, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(data.Date))
            data.Date = DateTimeOffset.UtcNow.ToString("MMMM d, yyyy");

        var pdfBytes = await _resumeGenerator.GenerateCoverLetterPdfAsync(data, ct);
        var filename = $"{SafeFilename(data.Name)}_cover_letter_{DateTimeOffset.UtcNow:yyyyMMdd_HHmmss}.pdf";
        var userId = GetUserId();
        var storagePath = await _documentStorage.SaveAsync(pdfBytes, filename, userId.ToString(), ct);

        var doc = new Document
        {
            UserId = userId,
            Type = "cover_letter",
            Filename = filename,
            StoragePath = storagePath,
            GeneratedAt = DateTimeOffset.UtcNow
        };
        _context.Documents.Add(doc);
        await _context.SaveChangesAsync(ct);

        return File(pdfBytes, MediaTypeNames.Application.Pdf, filename);
    }

    [HttpPost("cover-letter/ai")]
    public async Task<IActionResult> GenerateCoverLetterWithAi([FromBody] GenerateCoverLetterAiRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        var profile = await _context.UserProfiles.AsNoTracking().Include(p => p.User).FirstOrDefaultAsync(p => p.UserId == userId, ct);
        var job = await _context.Jobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == request.JobId && j.UserId == userId, ct);

        if (job == null)
            return BadRequest(new ProblemDetails { Title = "Job not found", Detail = "The specified job does not exist or does not belong to you." });

        var opening = request.Opening;
        var body = request.Body;
        var closing = request.Closing;

        // Generate content with AI when not provided
        if (string.IsNullOrWhiteSpace(opening) || string.IsNullOrWhiteSpace(body) || string.IsNullOrWhiteSpace(closing))
        {
            var skills = await _context.Skills.AsNoTracking().Where(s => s.UserId == userId).Select(s => s.Name).ToListAsync(ct);
            var experiences = await _context.WorkExperiences.AsNoTracking().Where(e => e.UserId == userId)
                .Select(e => e.Title + " at " + e.Company + ": " + e.Bullets).ToListAsync(ct);
            var education = await _context.Degrees.AsNoTracking().Include(d => d.Institution).Where(d => d.UserId == userId)
                .Select(d => d.DegreeName + " - " + (d.Institution != null ? d.Institution.Name : "")).ToListAsync(ct);

            var systemPrompt = "You are an expert cover letter writer following Harvard Career Services guidelines. You write concise, factual, tailored letters using only the candidate's real data — never inventing experience or skills.";
            var userPrompt = $@"Write a cover letter for this candidate and job. Harvard guidelines: address why you're a fit, highlight 1-2 key relevant examples (don't repeat the whole resume), confident tone, no flowery language, minimal use of 'I', plenty of action words, max one page.

CANDIDATE:
Name: {profile?.FullName ?? "Candidate"}
Summary: {profile?.Summary ?? ""}
Target Roles: {profile?.TargetRoles ?? ""}
Skills: {string.Join(", ", skills)}
Experience: {string.Join(" | ", experiences)}
Education: {string.Join(" | ", education)}

JOB:
Title: {job.Title}
Company: {job.Company}
Description: {job.Description ?? "(no description)"}

Return ONLY valid JSON:
{{
  ""opening"": ""<opening paragraph: state the position, why writing, 3 quick reasons for fit>"",
  ""body"": ""<1-2 middle paragraphs: relevant experience with concrete examples from the candidate's real history, tied to the job's needs>"",
  ""closing"": ""<closing paragraph: reiterate interest, thank reader, look forward to discussing>""
}}";

            try
            {
                var json = await _aiProvider.CompleteJsonAsync(systemPrompt, userPrompt, ct);
                var generated = JsonSerializer.Deserialize<CoverLetterAiContent>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (generated != null)
                {
                    opening ??= generated.Opening;
                    body ??= generated.Body;
                    closing ??= generated.Closing;
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new ProblemDetails { Title = "AI cover letter generation failed", Detail = ex.Message });
            }
        }

        var data = new CoverLetterDataDto
        {
            Name = profile?.FullName ?? "",
            Email = profile?.User?.Email ?? "",
            Phone = profile?.Phone ?? "",
            LinkedInUrl = profile?.LinkedInUrl ?? "",
            GitHubUrl = profile?.GitHubUrl ?? "",
            PortfolioUrl = profile?.PortfolioUrl ?? "",
            Company = job.Company ?? "",
            JobTitle = job.Title ?? "",
            Date = DateTimeOffset.UtcNow.ToString("MMMM d, yyyy"),
            Opening = opening ?? "",
            Body = body ?? "",
            Closing = closing ?? ""
        };

        var pdfBytes = await _resumeGenerator.GenerateCoverLetterPdfAsync(data, ct);
        var filename = $"{SafeFilename(data.Name)}_cover_letter_{job.Company}_{DateTimeOffset.UtcNow:yyyyMMdd_HHmmss}.pdf";
        var storagePath = await _documentStorage.SaveAsync(pdfBytes, filename, userId.ToString(), ct);

        var doc = new Document
        {
            UserId = userId,
            JobId = job.Id,
            Type = "cover_letter",
            Filename = filename,
            StoragePath = storagePath,
            GeneratedAt = DateTimeOffset.UtcNow
        };
        _context.Documents.Add(doc);
        await _context.SaveChangesAsync(ct);

        return File(pdfBytes, MediaTypeNames.Application.Pdf, filename);
    }

    [HttpGet]
    public async Task<ActionResult<List<GeneratedDocumentDto>>> GetDocuments(CancellationToken ct)
    {
        var userId = GetUserId();
        var docs = await _context.Documents
            .AsNoTracking()
            .Where(d => d.UserId == userId)
            .OrderByDescending(d => d.GeneratedAt)
            .Select(d => new GeneratedDocumentDto
            {
                Id = d.Id,
                Type = d.Type,
                Filename = d.Filename,
                GeneratedAt = d.GeneratedAt,
                JobId = d.JobId,
                JobTitle = d.Job != null ? d.Job.Title : null
            })
            .ToListAsync(ct);

        return docs;
    }

    [HttpGet("{id}/download")]
    public async Task<IActionResult> DownloadDocument(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        var doc = await _context.Documents.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id && d.UserId == userId, ct);
        if (doc == null)
            return NotFound();

        var bytes = await _documentStorage.LoadAsync(doc.StoragePath, ct);
        return File(bytes, MediaTypeNames.Application.Pdf, doc.Filename);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteDocument(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        var doc = await _context.Documents.FirstOrDefaultAsync(d => d.Id == id && d.UserId == userId, ct);
        if (doc == null)
            return NotFound();

        _documentStorage.Delete(doc.StoragePath);
        _context.Documents.Remove(doc);
        await _context.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpGet("resume-data")]
    public async Task<ActionResult<ResumeDataDto>> GetResumeData(CancellationToken ct)
    {
        var userId = GetUserId();
        var data = await _resumeDataBuilder.BuildAsync(userId, ct);
        return data;
    }

    private static string SafeFilename(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "resume";
        return string.Concat(name.Where(c => char.IsLetterOrDigit(c) || c == '_' || c == '-')).Trim();
    }
}

public class GenerateResumeRequest
{
    public string? Title { get; set; }
    public string? Template { get; set; }
    public List<string>? SectionOrder { get; set; }
    public ResumeDataDto? Data { get; set; }
}

public class GenerateCoverLetterAiRequest
{
    public Guid JobId { get; set; }
    public string? Opening { get; set; }
    public string? Body { get; set; }
    public string? Closing { get; set; }
}

public class CoverLetterAiContent
{
    public string Opening { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Closing { get; set; } = string.Empty;
}
