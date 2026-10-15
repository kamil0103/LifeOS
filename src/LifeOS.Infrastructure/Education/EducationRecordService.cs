using LifeOS.Application.DTOs.Education;
using LifeOS.Application.Interfaces;
using LifeOS.Domain.Entities;
using LifeOS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
namespace LifeOS.Infrastructure.Education;

public class EducationRecordService(AppDbContext db) : IEducationRecordService
{
    private async Task CheckInstitutionAsync(Guid userId, Guid? id, CancellationToken ct)
    {
        if (id.HasValue && !await db.Institutions.AnyAsync(i => i.Id == id && i.UserId == userId, ct))
            throw new InvalidOperationException("Select one of your institutions.");
    }
    public async Task<DegreeDto?> SaveDegreeAsync(Guid userId, Guid? id, CreateDegreeRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.DegreeName) || request.DegreeName.Length > 300) throw new InvalidOperationException("Enter a degree name of at most 300 characters.");
        var degree = id.HasValue ? await db.Degrees.Include(d => d.Courses).FirstOrDefaultAsync(d => d.Id == id && d.UserId == userId, ct) : new Degree { UserId = userId };
        if (degree == null) return null;
        await CheckInstitutionAsync(userId, request.InstitutionId, ct);
        degree.InstitutionId = request.InstitutionId; degree.DegreeName = request.DegreeName.Trim(); degree.DegreeType = request.DegreeType;
        degree.Field = request.Field; degree.StartDate = request.StartDate; degree.EndDate = request.IsCurrent ? null : request.EndDate; degree.IsCurrent = request.IsCurrent;
        degree.Gpa = request.Gpa; degree.Honors = request.Honors;
        foreach (var course in degree.Courses.Where(c => c.UserId == userId)) course.InstitutionId = degree.InstitutionId;
        if (!id.HasValue) db.Degrees.Add(degree);
        await db.SaveChangesAsync(ct);
        return new() { Id = degree.Id, InstitutionId = degree.InstitutionId, DegreeName = degree.DegreeName, DegreeType = degree.DegreeType, Field = degree.Field,
            StartDate = degree.StartDate, EndDate = degree.EndDate, IsCurrent = degree.IsCurrent, Gpa = degree.Gpa, Honors = degree.Honors, Courses = degree.Courses.Where(c => c.UserId == userId).Select(Map).ToList() };
    }
    public async Task<CourseDto?> SaveCourseAsync(Guid userId, Guid? id, CreateCourseRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 300) throw new InvalidOperationException("Enter a course name of at most 300 characters.");
        if (request.Credits is < 0 or > 1000) throw new InvalidOperationException("Course credits must be between 0 and 1000.");
        var course = id.HasValue ? await db.Courses.FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId, ct) : new Course { UserId = userId };
        if (course == null) return null;
        await CheckInstitutionAsync(userId, request.InstitutionId, ct);
        var institutionId = request.InstitutionId;
        if (request.DegreeId.HasValue)
        {
            var degree = await db.Degrees.FirstOrDefaultAsync(d => d.Id == request.DegreeId && d.UserId == userId, ct)
                ?? throw new InvalidOperationException("Select one of your degrees.");
            if (institutionId.HasValue && institutionId != degree.InstitutionId) throw new InvalidOperationException("The course institution must match its degree.");
            institutionId = degree.InstitutionId;
        }
        course.DegreeId = request.DegreeId; course.InstitutionId = institutionId; course.Name = request.Name.Trim(); course.Code = request.Code?.Trim();
        course.Grade = request.Grade; course.Credits = request.Credits; course.Term = request.Term; course.Description = request.Description; course.IsMajorRelated = request.IsMajorRelated;
        if (!id.HasValue) db.Courses.Add(course);
        await db.SaveChangesAsync(ct); return Map(course);
    }
    public async Task<bool> DeleteDegreeAsync(Guid userId, Guid id, CancellationToken ct)
    {
        var degree = await db.Degrees.Include(d => d.Courses).FirstOrDefaultAsync(d => d.Id == id && d.UserId == userId, ct);
        if (degree == null) return false;
        foreach (var course in degree.Courses) { course.DegreeId = null; if (course.UserId == userId) course.InstitutionId = degree.InstitutionId; }
        db.Degrees.Remove(degree); await db.SaveChangesAsync(ct); return true;
    }
    private static CourseDto Map(Course c) => new() { Id = c.Id, DegreeId = c.DegreeId, InstitutionId = c.InstitutionId, Code = c.Code, Name = c.Name, Grade = c.Grade,
        Credits = c.Credits, Term = c.Term, Description = c.Description, IsMajorRelated = c.IsMajorRelated };
}
