using LifeOS.Application.DTOs.Education;
using LifeOS.Domain.Entities;
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

    public EducationController(AppDbContext context)
    {
        _context = context;
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
            .FirstOrDefaultAsync(i => i.Id == id && i.UserId == userId, ct);

        if (institution == null) return NotFound();

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
    {
        var userId = GetUserId();
        var degree = new Degree
        {
            UserId = userId,
            InstitutionId = request.InstitutionId,
            DegreeName = request.DegreeName,
            DegreeType = request.DegreeType,
            Field = request.Field,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Gpa = request.Gpa,
            Honors = request.Honors,
            IsCurrent = request.IsCurrent
        };

        _context.Degrees.Add(degree);
        await _context.SaveChangesAsync(ct);
        return Ok(MapDegree(degree));
    }

    [HttpPut("degrees/{id:guid}")]
    public async Task<ActionResult<DegreeDto>> UpdateDegree(Guid id, UpdateDegreeRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        var degree = await _context.Degrees
            .FirstOrDefaultAsync(d => d.Id == id && d.UserId == userId, ct);

        if (degree == null) return NotFound();

        degree.InstitutionId = request.InstitutionId;
        degree.DegreeName = request.DegreeName;
        degree.DegreeType = request.DegreeType;
        degree.Field = request.Field;
        degree.StartDate = request.StartDate;
        degree.EndDate = request.EndDate;
        degree.Gpa = request.Gpa;
        degree.Honors = request.Honors;
        degree.IsCurrent = request.IsCurrent;

        await _context.SaveChangesAsync(ct);
        return Ok(MapDegree(degree));
    }

    [HttpDelete("degrees/{id:guid}")]
    public async Task<IActionResult> DeleteDegree(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        var degree = await _context.Degrees
            .FirstOrDefaultAsync(d => d.Id == id && d.UserId == userId, ct);

        if (degree == null) return NotFound();

        _context.Degrees.Remove(degree);
        await _context.SaveChangesAsync(ct);
        return NoContent();
    }

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
    {
        var userId = GetUserId();
        var course = new Course
        {
            UserId = userId,
            DegreeId = request.DegreeId,
            InstitutionId = request.InstitutionId,
            Code = request.Code,
            Name = request.Name,
            Grade = request.Grade,
            Credits = request.Credits,
            Term = request.Term,
            Description = request.Description,
            IsMajorRelated = request.IsMajorRelated
        };

        _context.Courses.Add(course);
        await _context.SaveChangesAsync(ct);
        return Ok(MapCourse(course));
    }

    [HttpPut("courses/{id:guid}")]
    public async Task<ActionResult<CourseDto>> UpdateCourse(Guid id, UpdateCourseRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        var course = await _context.Courses
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId, ct);

        if (course == null) return NotFound();

        course.DegreeId = request.DegreeId;
        course.InstitutionId = request.InstitutionId;
        course.Code = request.Code;
        course.Name = request.Name;
        course.Grade = request.Grade;
        course.Credits = request.Credits;
        course.Term = request.Term;
        course.Description = request.Description;
        course.IsMajorRelated = request.IsMajorRelated;

        await _context.SaveChangesAsync(ct);
        return Ok(MapCourse(course));
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
        Degrees = i.Degrees.Select(MapDegree).ToList(),
        UnassignedCourses = i.UnassignedCourses.Select(MapCourse).ToList()
    };

    private DegreeDto MapDegree(Degree d) => new()
    {
        Id = d.Id,
        InstitutionId = d.InstitutionId,
        InstitutionName = d.Institution?.Name,
        DegreeName = d.DegreeName,
        DegreeType = d.DegreeType,
        Field = d.Field,
        StartDate = d.StartDate,
        EndDate = d.EndDate,
        Gpa = d.Gpa,
        Honors = d.Honors,
        IsCurrent = d.IsCurrent,
        Courses = d.Courses?.Select(MapCourse).ToList() ?? []
    };

    private static CourseDto MapCourse(Course c) => new()
    {
        Id = c.Id,
        DegreeId = c.DegreeId,
        Code = c.Code,
        Name = c.Name,
        Grade = c.Grade,
        Credits = c.Credits,
        Term = c.Term,
        Description = c.Description,
        IsMajorRelated = c.IsMajorRelated
    };
}
