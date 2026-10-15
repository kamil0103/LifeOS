using LifeOS.Application.DTOs.Education;
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
public class EducationController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IEducationRecordService _records;

    public EducationController(AppDbContext context, IEducationRecordService records)
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

    // ==================== INSTITUTIONS ====================
    [HttpGet("institutions")]
    public async Task<ActionResult<List<InstitutionDto>>> GetInstitutions(CancellationToken ct)
    {
        var userId = GetUserId();
        var institutions = await _context.Institutions
            .AsNoTracking()
            .Where(i => i.UserId == userId)
            .Include(i => i.Degrees)
            .ThenInclude(d => d.Courses)
            .Include(i => i.UnassignedCourses)
            .ToListAsync(ct);

        return Ok(institutions.Select(MapInstitution));
    }

    [HttpPost("institutions")]
    public async Task<ActionResult<InstitutionDto>> CreateInstitution(CreateInstitutionRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        var institution = new Institution
        {
            UserId = userId,
            Name = request.Name,
            InstitutionType = request.InstitutionType,
            Location = request.Location
        };

        _context.Institutions.Add(institution);
        await _context.SaveChangesAsync(ct);
        return Ok(MapInstitution(institution));
    }

    [HttpPut("institutions/{id:guid}")]
    public async Task<ActionResult<InstitutionDto>> UpdateInstitution(Guid id, UpdateInstitutionRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        var institution = await _context.Institutions
            .FirstOrDefaultAsync(i => i.Id == id && i.UserId == userId, ct);

        if (institution == null) return NotFound();

        institution.Name = request.Name;
        institution.InstitutionType = request.InstitutionType;
        institution.Location = request.Location;
        await _context.SaveChangesAsync(ct);

        return Ok(MapInstitution(institution));
    }

    [HttpDelete("institutions/{id:guid}")]
    public async Task<IActionResult> DeleteInstitution(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        var institution = await _context.Institutions
            .Include(i => i.Degrees)
            .Include(i => i.UnassignedCourses)
            .FirstOrDefaultAsync(i => i.Id == id && i.UserId == userId, ct);

        if (institution == null) return NotFound();

        // Remove unassigned courses and degrees' courses explicitly to avoid FK violations
        var degrees = await _context.Degrees
            .Include(d => d.Courses)
            .Where(d => d.InstitutionId == id && d.UserId == userId)
            .ToListAsync(ct);

        foreach (var degree in degrees)
        {
            _context.Courses.RemoveRange(degree.Courses);
        }
        _context.Courses.RemoveRange(institution.UnassignedCourses);
        _context.Degrees.RemoveRange(degrees);
        _context.Institutions.Remove(institution);
        await _context.SaveChangesAsync(ct);
        return NoContent();
    }

    // ==================== DEGREES ====================
    [HttpGet("degrees")]
    public async Task<ActionResult<List<DegreeDto>>> GetDegrees(CancellationToken ct)
    {
        var userId = GetUserId();
        var degrees = await _context.Degrees
            .AsNoTracking()
            .Where(d => d.UserId == userId)
            .Include(d => d.Institution)
            .Include(d => d.Courses)
            .ToListAsync(ct);

        return Ok(degrees.Select(MapDegree));
    }

    [HttpPost("degrees")]
    public async Task<ActionResult<DegreeDto>> CreateDegree(CreateDegreeRequest request, CancellationToken ct)
        => Ok(await _records.SaveDegreeAsync(GetUserId(), null, request, ct));

    [HttpPut("degrees/{id:guid}")]
    public async Task<ActionResult<DegreeDto>> UpdateDegree(Guid id, UpdateDegreeRequest request, CancellationToken ct)
    {
        var result = await _records.SaveDegreeAsync(GetUserId(), id, new() { InstitutionId = request.InstitutionId, DegreeName = request.DegreeName, DegreeType = request.DegreeType,
            Field = request.Field, StartDate = request.StartDate, EndDate = request.EndDate, Gpa = request.Gpa, Honors = request.Honors, IsCurrent = request.IsCurrent }, ct);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpDelete("degrees/{id:guid}")]
    public async Task<IActionResult> DeleteDegree(Guid id, CancellationToken ct)
        => await _records.DeleteDegreeAsync(GetUserId(), id, ct) ? NoContent() : NotFound();

    // ==================== COURSES ====================
    [HttpGet("courses")]
    public async Task<ActionResult<List<CourseDto>>> GetCourses(CancellationToken ct)
    {
        var userId = GetUserId();
        var courses = await _context.Courses
            .AsNoTracking()
            .Where(c => c.UserId == userId)
            .ToListAsync(ct);

        return Ok(courses.Select(MapCourse));
    }

    [HttpPost("courses")]
    public async Task<ActionResult<CourseDto>> CreateCourse(CreateCourseRequest request, CancellationToken ct)
        => Ok(await _records.SaveCourseAsync(GetUserId(), null, request, ct));

    [HttpPut("courses/{id:guid}")]
    public async Task<ActionResult<CourseDto>> UpdateCourse(Guid id, UpdateCourseRequest request, CancellationToken ct)
    {
        var result = await _records.SaveCourseAsync(GetUserId(), id, new() { DegreeId = request.DegreeId, InstitutionId = request.InstitutionId, Code = request.Code, Name = request.Name,
            Grade = request.Grade, Credits = request.Credits, Term = request.Term, Description = request.Description, IsMajorRelated = request.IsMajorRelated }, ct);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpDelete("courses/{id:guid}")]
    public async Task<IActionResult> DeleteCourse(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        var course = await _context.Courses
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId, ct);

        if (course == null) return NotFound();

        _context.Courses.Remove(course);
        await _context.SaveChangesAsync(ct);
        return NoContent();
    }

    // ==================== MAPPERS ====================
    private InstitutionDto MapInstitution(Institution i) => new()
    {
        Id = i.Id,
        Name = i.Name,
        InstitutionType = i.InstitutionType,
        Location = i.Location,
        Degrees = i.Degrees.Where(d => d.UserId == i.UserId).Select(MapDegree).ToList(),
        UnassignedCourses = i.UnassignedCourses.Where(c => c.UserId == i.UserId && c.DegreeId == null).Select(MapCourse).ToList()
    };

    private DegreeDto MapDegree(Degree d) => new()
    {
        Id = d.Id,
        InstitutionId = d.InstitutionId,
        InstitutionName = d.Institution?.UserId == d.UserId ? d.Institution.Name : null,
        DegreeName = d.DegreeName,
        DegreeType = d.DegreeType,
        Field = d.Field,
        StartDate = d.StartDate,
        EndDate = d.EndDate,
        Gpa = d.Gpa,
        Honors = d.Honors,
        IsCurrent = d.IsCurrent,
        Courses = d.Courses?.Where(c => c.UserId == d.UserId).Select(MapCourse).ToList() ?? []
    };

    private static CourseDto MapCourse(Course c) => new()
    {
        Id = c.Id,
        DegreeId = c.DegreeId,
        InstitutionId = c.InstitutionId,
        Code = c.Code,
        Name = c.Name,
        Grade = c.Grade,
        Credits = c.Credits,
        Term = c.Term,
        Description = c.Description,
        IsMajorRelated = c.IsMajorRelated
    };
}
