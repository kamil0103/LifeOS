using LifeOS.Application.DTOs.Skills;
using LifeOS.Application.Interfaces;
using LifeOS.Domain.Entities;
using LifeOS.Infrastructure.Data;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SkillsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IProfessionalRecordService _records;

    public SkillsController(AppDbContext context, IProfessionalRecordService records)
    {
        _context = context;
        _records = records;
    }

    private Guid GetUserId()
    {
        var sub = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        return Guid.Parse(sub!);
    }

    // ==================== SKILLS ====================
    [HttpGet]
    public async Task<ActionResult<List<SkillDto>>> GetSkills(CancellationToken ct)
    {
        var userId = GetUserId();
        var skills = await _context.Skills
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .OrderBy(s => s.Category)
            .ThenBy(s => s.Name)
            .ToListAsync(ct);

        return Ok(skills.Select(MapSkill));
    }

    [HttpPost]
    public async Task<ActionResult<SkillDto>> CreateSkill(CreateSkillRequest request, CancellationToken ct)
        => Ok(await _records.SaveSkillAsync(GetUserId(), null, request, ct));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<SkillDto>> UpdateSkill(Guid id, UpdateSkillRequest request, CancellationToken ct)
        => await _records.SaveSkillAsync(GetUserId(), id, request, ct) is { } result ? Ok(result) : NotFound();

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteSkill(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        var skill = await _context.Skills
            .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId, ct);

        if (skill == null) return NotFound();

        _context.Skills.Remove(skill);
        await _context.SaveChangesAsync(ct);
        return NoContent();
    }

    // ==================== CERTIFICATES ====================
    [HttpGet("certificates")]
    public async Task<ActionResult<List<CertificateDto>>> GetCertificates(CancellationToken ct)
        => Ok(await _records.CertificatesAsync(GetUserId(), ct));

    [HttpPost("certificates")]
    public async Task<ActionResult<CertificateDto>> CreateCertificate(CreateCertificateRequest request, CancellationToken ct)
        => Ok(await _records.SaveCertificateAsync(GetUserId(), null, request, ct));

    [HttpPut("certificates/{id:guid}")]
    public async Task<ActionResult<CertificateDto>> UpdateCertificate(Guid id, UpdateCertificateRequest request, CancellationToken ct)
        => await _records.SaveCertificateAsync(GetUserId(), id, request, ct) is { } result ? Ok(result) : NotFound();

    [HttpDelete("certificates/{id:guid}")]
    public async Task<IActionResult> DeleteCertificate(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        var cert = await _context.Certificates
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId, ct);

        if (cert == null) return NotFound();

        _context.Certificates.Remove(cert);
        await _context.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("extract-ai")]
    public async Task<ActionResult<List<ExtractedSkillDto>>> ExtractSkillsAi([FromServices] IAiProvider aiProvider, CancellationToken ct)
    {
        var userId = GetUserId();
        var courses = await _context.Courses.AsNoTracking().Where(c => c.UserId == userId).Select(c => c.Name).ToListAsync(ct);
        var experiences = await _context.WorkExperiences.AsNoTracking().Where(e => e.UserId == userId).Select(e => $"{e.Title} at {e.Company}: {e.Bullets}").ToListAsync(ct);

        if (!courses.Any() && !experiences.Any())
            return Ok(new List<ExtractedSkillDto>());

        var promptText = $"Extract technical and professional skills from the following courses and work experiences.\n\n" +
            $"Courses:\n{string.Join("\n", courses)}\n\n" +
            $"Work Experience:\n{string.Join("\n", experiences)}\n\n" +
            $"Return ONLY a JSON array of objects with structure: [{{\"name\": \"<skill name>\", \"category\": \"<Programming Language|Framework|Tool|Concept|Database|Cloud|DevOps|Other>\", \"proficiency\": \"Intermediate\"}}].";

        try
        {
            var json = await aiProvider.CompleteJsonAsync("You are an expert technical skills extractor.", promptText, ct);
            var cleaned = json.Trim();
            if (cleaned.StartsWith("```json")) cleaned = cleaned[7..];
            if (cleaned.StartsWith("```")) cleaned = cleaned[3..];
            if (cleaned.EndsWith("```")) cleaned = cleaned[..^3];
            cleaned = cleaned.Trim();

            var list = JsonSerializer.Deserialize<List<ExtractedSkillDto>>(cleaned, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
            return Ok(list);
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            return BadRequest(new ProblemDetails { Title = "AI Extraction Failed", Detail = "Could not extract skills. Try again or add skills manually." });
        }
    }

    [HttpPost("save-extracted")]
    public async Task<IActionResult> SaveExtractedSkills([FromBody] List<ExtractedSkillDto> skills, CancellationToken ct)
    {
        await _records.SaveExtractedAsync(GetUserId(), skills, ct); return NoContent();
    }
    private static SkillDto MapSkill(Skill s) => new()
    {
        Id = s.Id,
        Name = s.Name,
        Category = s.Category,
        Proficiency = s.Proficiency,
        Source = s.Source
    };

}
