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
    public async Task ApiRetryAndInvalidCreditsAreSafe()
    {
        using var app = new HabitApiTests.Factory(); using var client = app.CreateClient(); var request = Request();
        var response = await client.PostAsJsonAsync("/api/transcripts/save", request); Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        response = await client.PostAsJsonAsync("/api/transcripts/save", request); var result = await response.Content.ReadFromJsonAsync<LifeOS.Application.Interfaces.TranscriptImportResult>(); Assert.Equal(0, result!.CoursesAdded);
        request.Courses[0].Credits = "-2"; Assert.Equal(System.Net.HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/transcripts/save", request)).StatusCode);
    }
}
