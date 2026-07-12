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
public class ResumeVersionsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public ResumeVersionsController(AppDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    private Guid GetUserId()
    {
        return _currentUser.UserId ?? throw new InvalidOperationException("User not authenticated");
    }

    [HttpGet]
    public async Task<ActionResult<List<ResumeVersionDto>>> GetVersions(CancellationToken ct)
    {
        var userId = GetUserId();
        var versions = await _context.ResumeVersions
            .AsNoTracking()
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new ResumeVersionDto
            {
                Id = r.Id,
                Title = r.Title,
                Template = r.Template,
                CreatedAt = r.CreatedAt
            })
            .ToListAsync(ct);

        return versions;
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ResumeVersionDto>> GetVersion(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        var version = await _context.ResumeVersions.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId, ct);
        if (version == null)
            return NotFound();

        ResumeDataDto? data = null;
        if (!string.IsNullOrWhiteSpace(version.ContentJson))
        {
            try
            {
                data = JsonSerializer.Deserialize<ResumeDataDto>(version.ContentJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch { }
        }

        return new ResumeVersionDto
        {
            Id = version.Id,
            Title = version.Title,
            Template = version.Template,
            CreatedAt = version.CreatedAt,
            Data = data
        };
    }

    [HttpPost]
    public async Task<ActionResult<ResumeVersionDto>> SaveVersion([FromBody] SaveResumeVersionRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        var version = new ResumeVersion
        {
            UserId = userId,
            Title = request.Title,
            Template = request.Template,
            ContentJson = JsonSerializer.Serialize(request.Data)
        };

        _context.ResumeVersions.Add(version);
        await _context.SaveChangesAsync(ct);

        return new ResumeVersionDto
        {
            Id = version.Id,
            Title = version.Title,
            Template = version.Template,
            CreatedAt = version.CreatedAt,
            Data = request.Data
        };
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ResumeVersionDto>> UpdateVersion(Guid id, [FromBody] SaveResumeVersionRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        var version = await _context.ResumeVersions.FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId, ct);
        if (version == null)
            return NotFound();

        version.Title = request.Title;
        version.Template = request.Template;
        version.ContentJson = JsonSerializer.Serialize(request.Data);
        await _context.SaveChangesAsync(ct);

        return new ResumeVersionDto
        {
            Id = version.Id,
            Title = version.Title,
            Template = version.Template,
            CreatedAt = version.CreatedAt,
            Data = request.Data
        };
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteVersion(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        var version = await _context.ResumeVersions.FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId, ct);
        if (version == null)
            return NotFound();

        _context.ResumeVersions.Remove(version);
        await _context.SaveChangesAsync(ct);
        return NoContent();
    }
}
