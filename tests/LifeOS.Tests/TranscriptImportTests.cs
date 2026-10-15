using System.Net.Http.Json;
using LifeOS.Application.DTOs.Education;
using LifeOS.Infrastructure.Data;
using LifeOS.Infrastructure.Education;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace LifeOS.Tests;
public class TranscriptImportTests
{
    private static AppDbContext Database() => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static SaveExtractedRequest Request() => new() { Institution = new() { Name = "Example University" }, Degree = new() { Name = "BSc", Field = "Computing" }, Courses = [new() { Name = "Algorithms", Code = "CS 101", Credits = "3.5", Grade = "A", Term = "Fall 2025" }] };
    [Fact]
    public async Task RetryNormalizesIdentityAndPreservesExistingValues()
    {
        using var db = Database(); var service = new TranscriptImportService(db); var user = Guid.NewGuid(); var request = Request();
        var first = await service.SaveAsync(user, request, default); Assert.Equal(1, first.CoursesAdded);
        request.Institution.Name = " EXAMPLE   university "; request.Degree.Name = " bsc "; request.Courses[0].Code = "cs101"; request.Courses[0].Grade = "B";
        var retry = await service.SaveAsync(user, request, default);
        Assert.Equal(first.InstitutionId, retry.InstitutionId); Assert.Equal(first.DegreeId, retry.DegreeId); Assert.Equal(0, retry.CoursesAdded); Assert.Equal(1, retry.CoursesSkipped);
        Assert.Single(db.Institutions); Assert.Single(db.Degrees); Assert.Equal("A", db.Courses.Single().Grade);
        request.Courses[0].Term = "Spring 2026"; Assert.Equal(1, (await service.SaveAsync(user, request, default)).CoursesAdded); Assert.Equal(2, db.Courses.Count());
        await service.SaveAsync(Guid.NewGuid(), request, default); Assert.Equal(2, db.Institutions.Count());
    }
    [Fact]
    public async Task InvalidLaterRowLeavesNoPartialImportAndBatchDuplicatesAreSkipped()
    {
        using var db = Database(); var service = new TranscriptImportService(db); var request = Request(); var user = Guid.NewGuid();
        request.Courses.Add(new() { Name = "Invalid", Credits = "unknown" });
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveAsync(user, request, default)); Assert.Empty(db.Institutions); Assert.Empty(db.Degrees); Assert.Empty(db.Courses);
        request.Courses[1] = request.Courses[0]; var result = await service.SaveAsync(user, request, default); Assert.Equal(1, result.CoursesAdded); Assert.Equal(1, result.CoursesSkipped);
    }
    [Fact]
    public async Task PreviewIsReadOnlyAndDistinguishesExistingBatchDuplicatesAndRetakes()
    {
        using var db = Database(); var service = new TranscriptImportService(db); var user = Guid.NewGuid(); var request = Request();
        var preview = await service.PreviewAsync(user, request, default); Assert.Equal("new", preview[0].Status); Assert.Empty(db.Institutions); Assert.Empty(db.Courses);
        await service.SaveAsync(user, request, default);
        request.Courses[0].Grade = "B";
        request.Courses.Add(new() { Name = "Databases", Code = "CS202", Term = "Fall 2025" });
        request.Courses.Add(new() { Name = "Databases", Code = "cs 202", Term = "Fall 2025" });
        request.Courses.Add(new() { Name = "Algorithms", Code = "CS101", Term = "Spring 2026" });
        preview = await service.PreviewAsync(user, request, default);
        Assert.Equal(new[] { "existing", "new", "batch_duplicate", "new" }, preview.Select(x => x.Status)); Assert.Equal("A", preview[0].ExistingCourse!.Grade); Assert.Equal(1, preview[2].DuplicateOf); Assert.Single(db.Courses);
        var foreign = await service.PreviewAsync(Guid.NewGuid(), request, default); Assert.Null(foreign[0].ExistingCourse); Assert.Equal("new", foreign[0].Status);
        request.Degree.Name = "Different degree"; preview = await service.PreviewAsync(user, request, default); Assert.Equal("new", preview[0].Status);
    }
    [Fact]
    public async Task DestinationSelectionEnforcesOwnershipAndDegreeAlignment()
    {
        using var db = Database(); var service = new TranscriptImportService(db); var user = Guid.NewGuid(); var request = Request();
        var first = await service.SaveAsync(user, request, default);
        request.Institution.Name = "Different extracted label"; request.Degree.Name = "Different extracted degree"; request.InstitutionId = first.InstitutionId; request.DegreeId = first.DegreeId;
        var preview = await service.PreviewAsync(user, request, default); Assert.Equal("existing", preview[0].Status);
        await service.SaveAsync(user, request, default); Assert.Single(db.Institutions); Assert.Single(db.Degrees);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.PreviewAsync(Guid.NewGuid(), request, default));
        var other = new LifeOS.Domain.Entities.Institution { UserId = user, Name = "Other school" }; db.Institutions.Add(other); await db.SaveChangesAsync(); request.InstitutionId = other.Id;
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveAsync(user, request, default)); Assert.Single(db.Courses);
        request.InstitutionId = first.InstitutionId; request.DegreeId = null; request.Unassigned = true;
        Assert.Equal("new", (await service.PreviewAsync(user, request, default))[0].Status);
        var unassigned = await service.SaveAsync(user, request, default); Assert.Null(unassigned.DegreeId); Assert.Single(db.Degrees); Assert.Equal(2, db.Courses.Count());
    }
    [Fact]
    public async Task ExplicitUpdatesAreRetrySafeAndPreserveOtherCourseFields()
    {
        using var db = Database(); var service = new TranscriptImportService(db); var user = Guid.NewGuid(); var request = Request();
        await service.SaveAsync(user, request, default); var saved = db.Courses.Single(); saved.Description = "Evidence"; saved.IsMajorRelated = false; await db.SaveChangesAsync();
        var preview = (await service.PreviewAsync(user, request, default))[0]; var row = request.Courses[0]; row.Grade = "B"; row.Credits = "4";
        row.UpdateExisting = true; row.ExistingCourseId = preview.ExistingCourse!.Id; row.ExpectedFingerprint = preview.Fingerprint;
        Assert.Equal(1, (await service.SaveAsync(user, request, default)).CoursesUpdated); Assert.Equal("B", saved.Grade); Assert.Equal(4m, saved.Credits); Assert.Equal("Evidence", saved.Description); Assert.False(saved.IsMajorRelated);
        var retry = await service.SaveAsync(user, request, default); Assert.Equal(0, retry.CoursesUpdated); Assert.Equal(1, retry.CoursesSkipped); Assert.Single(db.Courses);
        preview = (await service.PreviewAsync(user, request, default))[0]; row.ExpectedFingerprint = preview.Fingerprint; row.Grade = ""; row.Credits = "";
        await service.SaveAsync(user, request, default); Assert.Equal("B", saved.Grade); Assert.Equal(4m, saved.Credits);
    }
    [Fact]
    public async Task StaleApprovalRejectsTheWholeImportBeforeAnyChanges()
    {
        using var db = Database(); var service = new TranscriptImportService(db); var user = Guid.NewGuid(); var request = Request();
        await service.SaveAsync(user, request, default); var preview = (await service.PreviewAsync(user, request, default))[0];
        var records = new EducationRecordService(db); var saved = db.Courses.Single();
        await records.SaveCourseAsync(user, saved.Id, new() { Name = saved.Name, Code = saved.Code, Term = saved.Term, InstitutionId = saved.InstitutionId, DegreeId = saved.DegreeId, Grade = "C", Credits = 3.5m }, default);
        var row = request.Courses[0]; row.Grade = "B"; row.UpdateExisting = true; row.ExistingCourseId = saved.Id; row.ExpectedFingerprint = preview.Fingerprint;
        request.Courses.Insert(0, new() { Name = "New course", Code = "CS202", Credits = "3" });
        await Assert.ThrowsAsync<LifeOS.Application.Interfaces.RecordConflictException>(() => service.SaveAsync(user, request, default)); Assert.Single(db.Courses); Assert.Equal("C", saved.Grade);
        row.ExpectedFingerprint = null; await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveAsync(user, request, default));
    }
    [Fact]
    public async Task ApiBindsDestinationAndRejectsAStaleApproval()
    {
        using var app = new HabitApiTests.Factory(); using var client = app.CreateClient(); var request = Request();
        var saved = await (await client.PostAsJsonAsync("/api/transcripts/save", request)).Content.ReadFromJsonAsync<LifeOS.Application.Interfaces.TranscriptImportResult>();
        request.InstitutionId = saved!.InstitutionId; request.DegreeId = saved.DegreeId;
        var preview = await (await client.PostAsJsonAsync("/api/transcripts/preview", request)).Content.ReadFromJsonAsync<List<TranscriptCoursePreview>>();
        var row = request.Courses[0]; row.UpdateExisting = true; row.ExistingCourseId = preview![0].ExistingCourse!.Id; row.ExpectedFingerprint = preview[0].Fingerprint; row.Grade = "B";
        var updated = await (await client.PostAsJsonAsync("/api/transcripts/save", request)).Content.ReadFromJsonAsync<LifeOS.Application.Interfaces.TranscriptImportResult>(); Assert.Equal(1, updated!.CoursesUpdated);
        row.Grade = "C"; Assert.Equal(System.Net.HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/transcripts/save", request)).StatusCode);
        request.InstitutionId = Guid.NewGuid(); Assert.Equal(System.Net.HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/transcripts/preview", request)).StatusCode);
    }
    [Fact]
    public async Task ApiRetryAndInvalidCreditsAreSafe()
    {
        using var app = new HabitApiTests.Factory(); using var client = app.CreateClient(); var request = Request();
        var previewResponse = await client.PostAsJsonAsync("/api/transcripts/preview", request); Assert.Equal(System.Net.HttpStatusCode.OK, previewResponse.StatusCode);
        var response = await client.PostAsJsonAsync("/api/transcripts/save", request); Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        response = await client.PostAsJsonAsync("/api/transcripts/save", request); var result = await response.Content.ReadFromJsonAsync<LifeOS.Application.Interfaces.TranscriptImportResult>(); Assert.Equal(0, result!.CoursesAdded);
        request.Courses[0].Credits = "-2"; Assert.Equal(System.Net.HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/transcripts/save", request)).StatusCode);
    }
}
