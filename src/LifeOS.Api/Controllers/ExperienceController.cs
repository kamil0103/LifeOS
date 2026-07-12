using LifeOS.Application.DTOs.Experience;
using LifeOS.Domain.Entities;
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

    public ExperienceController(AppDbContext context)
    {
        _context = context;
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
    {
        var userId = GetUserId();
        var experience = new WorkExperience
        {
            UserId = userId,
            Company = request.Company,
            Title = request.Title,
            Location = request.Location,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            IsCurrent = request.IsCurrent,
            Bullets = request.Bullets
        };

        _context.WorkExperiences.Add(experience);
        await _context.SaveChangesAsync(ct);
        return Ok(MapWorkExperience(experience));
    }

    [HttpPut("work/{id:guid}")]
    public async Task<ActionResult<WorkExperienceDto>> UpdateWorkExperience(Guid id, UpdateWorkExperienceRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        var experience = await _context.WorkExperiences
            .FirstOrDefaultAsync(w => w.Id == id && w.UserId == userId, ct);

        if (experience == null) return NotFound();

        experience.Company = request.Company;
        experience.Title = request.Title;
        experience.Location = request.Location;
        experience.StartDate = request.StartDate;
        experience.EndDate = request.EndDate;
        experience.IsCurrent = request.IsCurrent;
        experience.Bullets = request.Bullets;

        await _context.SaveChangesAsync(ct);
        return Ok(MapWorkExperience(experience));
    }

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
    {
        var userId = GetUserId();
        var project = new Project
        {
            UserId = userId,
            Name = request.Name,
            Description = request.Description,
            Technologies = request.Technologies,
            Link = request.Link,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            IsCurrent = request.IsCurrent,
            IsPortfolioProject = request.IsPortfolioProject
        };

        _context.Projects.Add(project);
        await _context.SaveChangesAsync(ct);
        return Ok(MapProject(project));
    }

    [HttpPut("projects/{id:guid}")]
    public async Task<ActionResult<ProjectDto>> UpdateProject(Guid id, UpdateProjectRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        var project = await _context.Projects
            .FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId, ct);

        if (project == null) return NotFound();

        project.Name = request.Name;
        project.Description = request.Description;
        project.Technologies = request.Technologies;
        project.Link = request.Link;
        project.StartDate = request.StartDate;
        project.EndDate = request.EndDate;
        project.IsCurrent = request.IsCurrent;
        project.IsPortfolioProject = request.IsPortfolioProject;

        await _context.SaveChangesAsync(ct);
        return Ok(MapProject(project));
    }

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
