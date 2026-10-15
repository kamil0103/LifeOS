using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using LifeOS.Application.DTOs.Education;
using LifeOS.Application.Interfaces;
using LifeOS.Domain.Entities;
using LifeOS.Infrastructure.Data;
using LifeOS.Infrastructure.Personal;
using Microsoft.EntityFrameworkCore;
namespace LifeOS.Infrastructure.Education;

public class TranscriptImportService(AppDbContext db) : ITranscriptImportService
{
    private static string Key(string? value) => Regex.Replace((value ?? "").Normalize(NormalizationForm.FormKC).Trim(), @"\s+", " ").ToUpperInvariant();
    private static string Code(string? value) => Key(value).Replace(" ", "");
    private static bool SameCourse(Course a, Course b) => Key(a.Term) == Key(b.Term) &&
        (Code(a.Code).Length > 0 && Code(b.Code).Length > 0 ? Code(a.Code) == Code(b.Code) : Key(a.Name) == Key(b.Name));

    // Read-only preview. Save independently rechecks the current database under its write lock.
    public async Task<IReadOnlyList<TranscriptCoursePreview>> PreviewAsync(Guid userId, SaveExtractedRequest request, CancellationToken ct)
    {
        if (request.Institution == null || Key(request.Institution.Name).Length == 0)
            throw new InvalidOperationException("Institution name is required.");
        if ((request.Courses?.Count ?? 0) > 2000) throw new InvalidOperationException("Import up to 2000 courses at a time.");
        var schools = (await db.Institutions.AsNoTracking().Where(x => x.UserId == userId).ToListAsync(ct)).Where(x => Key(x.Name) == Key(request.Institution.Name)).ToList();
        if (schools.Count > 1) throw new RecordConflictException("Multiple matching institutions exist. Rename them in Education before importing.");
        var school = schools.SingleOrDefault();
        Degree? degree = null;
        var hasDegree = Key(request.Degree?.Name).Length > 0;
        if (school != null && hasDegree)
        {
            var degrees = (await db.Degrees.AsNoTracking().Where(x => x.UserId == userId && x.InstitutionId == school.Id).ToListAsync(ct)).Where(x => Key(x.DegreeName) == Key(request.Degree!.Name) && Key(x.Field) == Key(request.Degree.Field)).ToList();
            if (degrees.Count > 1) throw new RecordConflictException("Multiple matching degrees exist. Rename them in Education before importing.");
            degree = degrees.SingleOrDefault();
        }
        var degreeId = degree?.Id;
        var existing = school == null || (hasDegree && degree == null) ? new List<Course>() : await db.Courses.AsNoTracking().Where(x => x.UserId == userId && x.InstitutionId == school.Id && x.DegreeId == degreeId).ToListAsync(ct);
        var seen = new List<Course>(); var result = new List<TranscriptCoursePreview>();
        foreach (var row in request.Courses ?? [])
        {
            if (row == null) throw new InvalidOperationException("Every imported course needs a name.");
            var course = new Course { Name = row.Name, Code = row.Code, Term = row.Term };
            var match = existing.FirstOrDefault(x => SameCourse(x, course));
            var duplicateOf = seen.FindIndex(x => SameCourse(x, course));
            result.Add(new(result.Count, match != null ? "existing" : duplicateOf >= 0 ? "batch_duplicate" : "new", match == null ? null : new CourseDto { Id = match.Id, Name = match.Name, Code = match.Code, Term = match.Term, Grade = match.Grade, Credits = match.Credits }, duplicateOf >= 0 ? duplicateOf : null));
            seen.Add(course);
        }
        return result;
    }

    public Task<TranscriptImportResult> SaveAsync(Guid userId, SaveExtractedRequest request, CancellationToken ct)
    {
        if (request.Institution == null || Key(request.Institution.Name).Length == 0)
            throw new InvalidOperationException("Institution name is required.");
        if ((request.Courses?.Count ?? 0) > 2000) throw new InvalidOperationException("Import up to 2000 courses at a time.");
        // Validate every row before tracking any changes. Invalid credits must never silently disappear.
        var incoming = new List<Course>();
        foreach (var row in request.Courses ?? [])
        {
            if (row == null || Key(row.Name).Length == 0) throw new InvalidOperationException("Every imported course needs a name.");
            decimal? credits = null;
            if (!string.IsNullOrWhiteSpace(row.Credits))
            {
                if (!decimal.TryParse(row.Credits, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var parsed) || parsed < 0 || parsed > 100)
                    throw new InvalidOperationException("Course credits must be a number between 0 and 100, using a decimal point.");
                credits = parsed;
            }
            incoming.Add(new Course { UserId = userId, Name = row.Name.Trim(), Code = row.Code?.Trim(), Grade = row.Grade?.Trim(), Credits = credits, Term = row.Term?.Trim() });
        }
        return PersonalWriteLock.RunAsync(db, userId, async () =>
        {
            var schools = (await db.Institutions.Where(x => x.UserId == userId).ToListAsync(ct)).Where(x => Key(x.Name) == Key(request.Institution.Name)).ToList();
            if (schools.Count > 1) throw new RecordConflictException("Multiple matching institutions exist. Rename them in Education before importing.");
            var school = schools.SingleOrDefault() ?? new Institution { UserId = userId, Name = request.Institution.Name.Trim(), InstitutionType = request.Institution.Type ?? "other", Location = request.Institution.Location };
            Degree? degree = null;
            if (Key(request.Degree?.Name).Length > 0)
            {
                var degrees = (await db.Degrees.Where(x => x.UserId == userId && x.InstitutionId == school.Id).ToListAsync(ct)).Where(x => Key(x.DegreeName) == Key(request.Degree!.Name) && Key(x.Field) == Key(request.Degree.Field)).ToList();
                if (degrees.Count > 1) throw new RecordConflictException("Multiple matching degrees exist. Rename them in Education before importing.");
                degree = degrees.SingleOrDefault() ?? new Degree { UserId = userId, InstitutionId = school.Id, DegreeName = request.Degree!.Name.Trim(), Field = request.Degree.Field, DegreeType = request.Degree.Type ?? "other", Gpa = request.Degree.Gpa, Honors = request.Degree.Honors };
            }
            var degreeId = degree?.Id;
            var existing = await db.Courses.Where(x => x.UserId == userId && x.InstitutionId == school.Id && x.DegreeId == degreeId).ToListAsync(ct);
            if (schools.Count == 0) db.Institutions.Add(school);
            if (degree != null && db.Entry(degree).State == EntityState.Detached) db.Degrees.Add(degree);
            var added = 0;
            foreach (var course in incoming)
            {
                // Existing values are preserved; retakes in distinct terms remain distinct records.
                if (existing.Any(x => SameCourse(x, course))) continue;
                course.InstitutionId = school.Id; course.DegreeId = degreeId;
                db.Courses.Add(course); existing.Add(course); added++;
            }
            return new TranscriptImportResult(school.Id, degreeId, added, incoming.Count - added);
        }, ct);
    }
}
