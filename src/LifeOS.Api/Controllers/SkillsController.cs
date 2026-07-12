using LifeOS.Application.DTOs.Skills;
using LifeOS.Domain.Entities;
using LifeOS.Infrastructure.Data;
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

    public SkillsController(AppDbContext context)
    {
        _context = context;
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
    {
        var userId = GetUserId();
        var skill = new Skill
        {
            UserId = userId,
            Name = request.Name,
            Category = request.Category,
            Proficiency = request.Proficiency,
            Source = request.Source
        };

        _context.Skills.Add(skill);
        await _context.SaveChangesAsync(ct);
        return Ok(MapSkill(skill));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<SkillDto>> UpdateSkill(Guid id, UpdateSkillRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        var skill = await _context.Skills
            .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId, ct);

        if (skill == null) return NotFound();

        skill.Name = request.Name;
        skill.Category = request.Category;
        skill.Proficiency = request.Proficiency;
        skill.Source = request.Source;

        await _context.SaveChangesAsync(ct);
        return Ok(MapSkill(skill));
    }

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
    {
        var userId = GetUserId();
        var certs = await _context.Certificates
            .AsNoTracking()
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.DateObtained)
            .ToListAsync(ct);

        return Ok(certs.Select(MapCertificate));
    }

    [HttpPost("certificates")]
    public async Task<ActionResult<CertificateDto>> CreateCertificate(CreateCertificateRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        var cert = new Certificate
        {
            UserId = userId,
            Name = request.Name,
            Issuer = request.Issuer,
            DateObtained = request.DateObtained,
            Expiry = request.Expiry,
            CredentialId = request.CredentialId,
            Url = request.Url,
            Description = request.Description
        };

        _context.Certificates.Add(cert);
        await _context.SaveChangesAsync(ct);
        return Ok(MapCertificate(cert));
    }

    [HttpPut("certificates/{id:guid}")]
    public async Task<ActionResult<CertificateDto>> UpdateCertificate(Guid id, UpdateCertificateRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        var cert = await _context.Certificates
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId, ct);

        if (cert == null) return NotFound();

        cert.Name = request.Name;
        cert.Issuer = request.Issuer;
        cert.DateObtained = request.DateObtained;
        cert.Expiry = request.Expiry;
        cert.CredentialId = request.CredentialId;
        cert.Url = request.Url;
        cert.Description = request.Description;

        await _context.SaveChangesAsync(ct);
        return Ok(MapCertificate(cert));
    }

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

    private static SkillDto MapSkill(Skill s) => new()
    {
        Id = s.Id,
        Name = s.Name,
        Category = s.Category,
        Proficiency = s.Proficiency,
        Source = s.Source
    };

    private static CertificateDto MapCertificate(Certificate c) => new()
    {
        Id = c.Id,
        Name = c.Name,
        Issuer = c.Issuer,
        DateObtained = c.DateObtained,
        Expiry = c.Expiry,
        CredentialId = c.CredentialId,
        Url = c.Url,
        Description = c.Description
    };
}
