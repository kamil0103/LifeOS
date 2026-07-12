using System.Net.Mime;
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

    public DocumentsController(
        AppDbContext context,
        ICurrentUserService currentUser,
        IResumeGenerator resumeGenerator,
        IResumeDataBuilder resumeDataBuilder,
        IDocumentStorage documentStorage)
    {
        _context = context;
        _currentUser = currentUser;
        _resumeGenerator = resumeGenerator;
        _resumeDataBuilder = resumeDataBuilder;
        _documentStorage = documentStorage;
    }

    private Guid GetUserId()
    {
        return _currentUser.UserId ?? throw new InvalidOperationException("User not authenticated");
    }

    [HttpPost("resume")]
    public async Task<IActionResult> GenerateResume([FromBody] GenerateResumeRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        var data = await _resumeDataBuilder.BuildAsync(userId, ct);
        data.Template = request.Template ?? data.Template;
        data.Title = request.Title ?? data.Title;
        data.SectionOrder = request.SectionOrder ?? data.SectionOrder;

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
            Opening = request.Opening ?? "",
            Body = request.Body ?? "",
            Closing = request.Closing ?? ""
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
}

public class GenerateCoverLetterAiRequest
{
    public Guid JobId { get; set; }
    public string? Opening { get; set; }
    public string? Body { get; set; }
    public string? Closing { get; set; }
}
