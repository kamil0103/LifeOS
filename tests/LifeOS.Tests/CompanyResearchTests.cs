using System.Net;
using System.Net.Http.Json;
using LifeOS.Application.DTOs.Jobs;
using LifeOS.Application.Interfaces;
using LifeOS.Domain.Entities;
using LifeOS.Infrastructure.Career;
using LifeOS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace LifeOS.Tests;
public class CompanyResearchTests
{
    [Fact]
    public async Task StructuredFieldsRoundTripAndOlderClientsPreserveResearch()
    {
        using var db = Database(); var user = Guid.NewGuid(); var s = new CompanyResearchService(db);
        var n = await s.SaveAsync(user, null, new() { CompanyName = " Router Co ", Website = "https://example.com", Industry = " Devices ", Products = "Routers", TechnologyStack = "C++", Culture = "Mentoring", InterviewPreparation = "Practice interrupts", Notes = "Legacy", Rating = 4 }, default);
        Assert.Equal("Router Co", n!.CompanyName); Assert.Equal("Devices", n.Industry);
        var old = await s.SaveAsync(user, n.Id, new() { CompanyName = n.CompanyName, Notes = "Updated legacy notes", Rating = 2 }, default);
        Assert.Equal("C++", old!.TechnologyStack); Assert.Equal("Practice interrupts", old.InterviewPreparation);
        var cleared = await s.SaveAsync(user, n.Id, new() { CompanyName = n.CompanyName, Website = "", ExpectedUpdatedAt = old.UpdatedAt }, default);
        Assert.Null(cleared!.Website); Assert.Equal("Routers", cleared.Products);
        var listed = Assert.Single(await s.ListAsync(user, default)); Assert.Equal("Mentoring", listed.Culture);
        Assert.Empty(await s.ListAsync(Guid.NewGuid(), default));
        Assert.Null(await s.SaveAsync(Guid.NewGuid(), n.Id, new() { CompanyName = "Foreign" }, default)); Assert.False(await s.DeleteAsync(Guid.NewGuid(), n.Id, default));
    }
    [Fact]
    public async Task ValidationAndStaleVersionCannotOverwriteSavedResearch()
    {
        using var db = Database(); var s = new CompanyResearchService(db); var user = Guid.NewGuid();
        var n = await s.SaveAsync(user, null, new() { CompanyName = "Company", Products = "Original" }, default);
        var updated = await s.SaveAsync(user, n!.Id, new() { CompanyName = "Company", Products = "New", ExpectedUpdatedAt = n.UpdatedAt }, default);
        await Assert.ThrowsAsync<RecordConflictException>(() => s.SaveAsync(user, n.Id, new() { CompanyName = "Company", Products = "Stale", ExpectedUpdatedAt = n.UpdatedAt }, default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => s.SaveAsync(user, n.Id, new() { CompanyName = "Bad", Website = "javascript:alert(1)" }, default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => s.SaveAsync(user, n.Id, new() { CompanyName = "Bad", Culture = new string('x', 10001) }, default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => s.SaveAsync(user, null, new() { CompanyName = "  " }, default));
        Assert.Equal("New", Assert.Single(await s.ListAsync(user, default)).Products);
    }
    [Fact]
    public async Task RelatedJobsUseExactNormalizedCompanyNamesAndOwnedApplications()
    {
        using var db = Database(); var user = Guid.NewGuid(); var s = new CompanyResearchService(db);
        var note = await s.SaveAsync(user, null, new() { CompanyName = "Router Co" }, default);
        var job = new Job { UserId = user, Company = " router CO ", Title = "Firmware", Status = "saved", SalaryRange = "Example range" };
        db.Jobs.AddRange(job, new() { UserId = user, Company = "Router Co Labs", Title = "Unrelated" }, new() { UserId = Guid.NewGuid(), Company = "Router Co", Title = "Private" });
        db.JobApplications.AddRange(new() { UserId = user, JobId = job.Id, Status = "interview" }, new() { UserId = Guid.NewGuid(), JobId = job.Id, Status = "offer" }); await db.SaveChangesAsync();
        var related = Assert.Single((await s.RelatedJobsAsync(user, note!.Id, default))!); Assert.Equal("Firmware", related.Title); Assert.Equal("interview", related.ApplicationStatus); Assert.NotNull(related.ApplicationId);
        Assert.Null(await s.RelatedJobsAsync(Guid.NewGuid(), note.Id, default)); Assert.True(await s.DeleteAsync(user, note.Id, default)); Assert.NotEmpty(db.Jobs);
    }
    [Fact]
    public async Task ApiValidatesStructuredFieldsAndProtectsRelatedJobOwnership()
    {
        using var app = new HabitApiTests.Factory(); using var client = app.CreateClient();
        var created = await client.PostAsJsonAsync("/api/companynotes", new { companyName = "Devices", products = "Sensors", website = "https://example.com" }); created.EnsureSuccessStatusCode(); var note = await created.Content.ReadFromJsonAsync<CompanyNoteDto>();
        Assert.Equal("Sensors", note!.Products); Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/companynotes", new { companyName = "Devices", website = "data:text/html,bad" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/companynotes", new { companyName = "Devices", industry = new string('x', 201) })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync("/api/companynotes/" + note.Id, new { companyName = "Devices", expectedUpdatedAt = note.UpdatedAt.AddDays(-1) })).StatusCode);
        Guid foreign;
        using (var scope = app.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var n = new CompanyNote { UserId = Guid.NewGuid(), CompanyName = "Private" }; db.CompanyNotes.Add(n); await db.SaveChangesAsync(); foreign = n.Id; }
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/companynotes/{foreign}/jobs")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/companynotes/{note.Id}/jobs")).StatusCode);
    }
    private static AppDbContext Database() => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
}
