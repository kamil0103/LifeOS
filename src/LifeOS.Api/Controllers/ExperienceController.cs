using LifeOS.Application.DTOs.Experience;
using LifeOS.Domain.Entities;
using LifeOS.Application.Interfaces;
using LifeOS.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ExperienceController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IProfessionalRecordService _records;

    public ExperienceController(AppDbContext context, IProfessionalRecordService records)
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

    // ==================== WORK EXPERIENCE ====================
    [HttpGet("work")]
    public async Task<ActionResult<List<WorkExperienceDto>>> GetWorkExperience(CancellationToken ct)
    {
        var userId = GetUserId();
        var experiences = await _context.WorkExperiences
            .AsNoTracking()
            .Where(w => w.UserId == userId)
            .OrderByDescending(w => w.StartDate)
            .ToListAsync(ct);

        return Ok(experiences.Select(MapWorkExperience));
    }

    [HttpPost("work")]
    public async Task<ActionResult<WorkExperienceDto>> CreateWorkExperience(CreateWorkExperienceRequest request, CancellationToken ct)
        => Ok(await _records.SaveWorkAsync(GetUserId(), null, request, ct));

    [HttpPut("work/{id:guid}")]
    public async Task<ActionResult<WorkExperienceDto>> UpdateWorkExperience(Guid id, UpdateWorkExperienceRequest request, CancellationToken ct)
        => await _records.SaveWorkAsync(GetUserId(), id, request, ct) is { } result ? Ok(result) : NotFound();

    [HttpDelete("work/{id:guid}")]
    public async Task<IActionResult> DeleteWorkExperience(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        var experience = await _context.WorkExperiences
            .FirstOrDefaultAsync(w => w.Id == id && w.UserId == userId, ct);

        if (experience == null) return NotFound();

        _context.WorkExperiences.Remove(experience);
        await _context.SaveChangesAsync(ct);
        return NoContent();
    }

    // ==================== PROJECTS ====================
    [HttpGet("projects")]
    public async Task<ActionResult<List<ProjectDto>>> GetProjects(CancellationToken ct)
    {
        var userId = GetUserId();
        var projects = await _context.Projects
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.StartDate)
            .ToListAsync(ct);

        return Ok(projects.Select(MapProject));
    }

    [HttpPost("projects")]
    public async Task<ActionResult<ProjectDto>> CreateProject(CreateProjectRequest request, CancellationToken ct)
        => Ok(await _records.SaveProjectAsync(GetUserId(), null, request, ct));

    [HttpPut("projects/{id:guid}")]
    public async Task<ActionResult<ProjectDto>> UpdateProject(Guid id, UpdateProjectRequest request, CancellationToken ct)
        => await _records.SaveProjectAsync(GetUserId(), id, request, ct) is { } result ? Ok(result) : NotFound();

    [HttpDelete("projects/{id:guid}")]
    public async Task<IActionResult> DeleteProject(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        var project = await _context.Projects
            .FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId, ct);

        if (project == null) return NotFound();

        _context.Projects.Remove(project);
        await _context.SaveChangesAsync(ct);
        return NoContent();
    }

    private static WorkExperienceDto MapWorkExperience(WorkExperience w) => new()
    {
        Id = w.Id,
        Company = w.Company,
        Title = w.Title,
        Location = w.Location,
        StartDate = w.StartDate,
        EndDate = w.EndDate,
        IsCurrent = w.IsCurrent,
        Bullets = w.Bullets
    };

    private static ProjectDto MapProject(Project p) => new()
    {
        Id = p.Id,
        Name = p.Name,
        Description = p.Description,
        Technologies = p.Technologies,
        Link = p.Link,
        StartDate = p.StartDate,
        EndDate = p.EndDate,
        IsCurrent = p.IsCurrent,
        IsPortfolioProject = p.IsPortfolioProject
    };
}
