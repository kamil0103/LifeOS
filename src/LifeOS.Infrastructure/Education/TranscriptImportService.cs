using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
    private static string Fingerprint(Course c) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { c.Id, c.InstitutionId, c.DegreeId, c.Name, c.Code, c.Term, c.Grade, c.Credits, c.Description, c.IsMajorRelated }))));

    private static void ValidateRequest(SaveExtractedRequest request)
    {
        if (!request.InstitutionId.HasValue && (request.Institution == null || Key(request.Institution.Name).Length == 0))
            throw new InvalidOperationException("Institution name or an existing institution is required.");
        if ((request.Courses?.Count ?? 0) > 2000) throw new InvalidOperationException("Import up to 2000 courses at a time.");
        if (request.DegreeId.HasValue && (!request.InstitutionId.HasValue || request.Unassigned))
            throw new InvalidOperationException("Select the institution for your degree, or choose unassigned courses.");
    }

    private async Task<(Institution School, Degree? Degree)> DestinationAsync(Guid userId, SaveExtractedRequest request, bool tracking, CancellationToken ct)
    {
        var institutions = tracking ? db.Institutions.AsQueryable() : db.Institutions.AsNoTracking();
        var degrees = tracking ? db.Degrees.AsQueryable() : db.Degrees.AsNoTracking();
        Institution school;
        if (request.InstitutionId.HasValue)
            school = await institutions.FirstOrDefaultAsync(x => x.UserId == userId && x.Id == request.InstitutionId, ct)
                ?? throw new InvalidOperationException("Select one of your institutions.");
        else
        {
            var matches = (await institutions.Where(x => x.UserId == userId).ToListAsync(ct)).Where(x => Key(x.Name) == Key(request.Institution.Name)).ToList();
            if (matches.Count > 1) throw new RecordConflictException("Multiple matching institutions exist. Select the destination institution explicitly.");
            school = matches.SingleOrDefault() ?? new Institution { UserId = userId, Name = request.Institution.Name.Trim(), InstitutionType = request.Institution.Type ?? "other", Location = request.Institution.Location };
        }
        if (request.Unassigned) return (school, null);
        if (request.DegreeId.HasValue)
        {
            var selected = await degrees.FirstOrDefaultAsync(x => x.UserId == userId && x.Id == request.DegreeId && x.InstitutionId == school.Id, ct)
                ?? throw new InvalidOperationException("Select a degree belonging to the destination institution.");
            return (school, selected);
        }
        if (Key(request.Degree?.Name).Length == 0) return (school, null);
        var degreeMatches = (await degrees.Where(x => x.UserId == userId && x.InstitutionId == school.Id).ToListAsync(ct)).Where(x => Key(x.DegreeName) == Key(request.Degree!.Name) && Key(x.Field) == Key(request.Degree.Field)).ToList();
        if (degreeMatches.Count > 1) throw new RecordConflictException("Multiple matching degrees exist. Select the destination degree explicitly.");
        return (school, degreeMatches.SingleOrDefault() ?? new Degree { UserId = userId, InstitutionId = school.Id, DegreeName = request.Degree!.Name.Trim(), Field = request.Degree.Field, DegreeType = request.Degree.Type ?? "other", Gpa = request.Degree.Gpa, Honors = request.Degree.Honors });
    }

    // Preview never tracks or writes records. Save independently checks the destination and current values.
    public async Task<IReadOnlyList<TranscriptCoursePreview>> PreviewAsync(Guid userId, SaveExtractedRequest request, CancellationToken ct)
    {
        ValidateRequest(request);
        var (school, degree) = await DestinationAsync(userId, request, false, ct); var degreeId = degree?.Id;
        var existing = await db.Courses.AsNoTracking().Where(x => x.UserId == userId && x.InstitutionId == school.Id && x.DegreeId == degreeId).ToListAsync(ct);
        var seen = new List<Course>(); var result = new List<TranscriptCoursePreview>();
        foreach (var row in request.Courses ?? [])
        {
            if (row == null) throw new InvalidOperationException("Every imported course needs a name.");
            var course = new Course { Name = row.Name, Code = row.Code, Term = row.Term };
            var match = existing.OrderBy(x => x.Id).FirstOrDefault(x => SameCourse(x, course));
            var duplicateOf = seen.FindIndex(x => SameCourse(x, course));
            result.Add(new(result.Count, match != null ? "existing" : duplicateOf >= 0 ? "batch_duplicate" : "new", match == null ? null : new CourseDto { Id = match.Id, Name = match.Name, Code = match.Code, Term = match.Term, Grade = match.Grade, Credits = match.Credits }, duplicateOf >= 0 ? duplicateOf : null, match == null ? null : Fingerprint(match)));
            seen.Add(course);
        }
        return result;
    }

    public async Task<TranscriptImportResult> SaveAsync(Guid userId, SaveExtractedRequest request, CancellationToken ct)
    {
        ValidateRequest(request);
        var incoming = new List<(Course Course, ExtractedCourseDto Row)>(); var updateIds = new HashSet<Guid>();
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
            if (row.UpdateExisting && (!row.ExistingCourseId.HasValue || string.IsNullOrWhiteSpace(row.ExpectedFingerprint) || !updateIds.Add(row.ExistingCourseId.Value)))
                throw new InvalidOperationException("Review each existing course once before approving its update.");
            incoming.Add((new Course { UserId = userId, Name = row.Name.Trim(), Code = row.Code?.Trim(), Grade = row.Grade?.Trim(), Credits = credits, Term = row.Term?.Trim() }, row));
        }
        try
        {
            return await PersonalWriteLock.RunAsync(db, userId, async () =>
            {
                var (school, degree) = await DestinationAsync(userId, request, true, ct); var degreeId = degree?.Id;
                var existing = await db.Courses.Where(x => x.UserId == userId && x.InstitutionId == school.Id && x.DegreeId == degreeId).ToListAsync(ct);
                // Validate all approvals before tracking new rows or changing existing values.
                foreach (var (course, row) in incoming.Where(x => x.Row.UpdateExisting))
                {
                    var target = existing.FirstOrDefault(x => x.Id == row.ExistingCourseId && SameCourse(x, course))
                        ?? throw new RecordConflictException("An approved course no longer matches this destination. Refresh the review.");
                    var grade = string.IsNullOrWhiteSpace(course.Grade) ? target.Grade : course.Grade;
                    var desiredCredits = course.Credits ?? target.Credits;
                    // A repeated successful request is a no-op, even if its original fingerprint is now old.
                    if ((target.Grade != grade || target.Credits != desiredCredits) && Fingerprint(target) != row.ExpectedFingerprint)
                        throw new RecordConflictException("A course changed after your review. Refresh the review before applying changes.");
                }
                if (db.Entry(school).State == EntityState.Detached) db.Institutions.Add(school);
                if (degree != null && db.Entry(degree).State == EntityState.Detached) db.Degrees.Add(degree);
                var added = 0; var updated = 0;
                foreach (var (course, row) in incoming)
                {
                    if (row.UpdateExisting)
                    {
                        var target = existing.Single(x => x.Id == row.ExistingCourseId);
                        var grade = string.IsNullOrWhiteSpace(course.Grade) ? target.Grade : course.Grade;
                        var desiredCredits = course.Credits ?? target.Credits;
                        if (target.Grade != grade || target.Credits != desiredCredits)
                        {
                            target.Grade = grade; target.Credits = desiredCredits; target.UpdatedAt = DateTimeOffset.UtcNow; updated++;
                        }
                    }
                    else if (!existing.Any(x => SameCourse(x, course)))
                    {
                        course.InstitutionId = school.Id; course.DegreeId = degreeId;
                        db.Courses.Add(course); existing.Add(course); added++;
                    }
                }
                return new TranscriptImportResult(school.Id, degreeId, added, incoming.Count - added - updated, updated);
            }, ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new RecordConflictException("A course changed or was removed while saving. Refresh the review.");
        }
    }
}
