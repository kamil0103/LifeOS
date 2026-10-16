using System.Net;
using System.Net.Http.Json;
using LifeOS.Application.DTOs.Skills;
using LifeOS.Application.DTOs.Experience;
using LifeOS.Application.DTOs.Portfolio;
using LifeOS.Application.Interfaces;
using LifeOS.Application.Services;
using LifeOS.Domain.Entities;
using LifeOS.Infrastructure.Data;
using LifeOS.Infrastructure.Professional;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.Hosting;
using Moq;
using Xunit;
namespace LifeOS.Tests;
public class ProfessionalRecordTests
{
    private sealed class Fixture : IDisposable
    {
        public readonly Guid User = Guid.NewGuid();
        public readonly AppDbContext Db = new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        public readonly Mock<IGitHubRepositoryClient> GitHub = GitHubMock();
        public readonly ProfessionalRecordService Service;
        public Fixture() { var days = new Mock<ILocalDayService>(); days.Setup(d => d.TodayAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(HabitApiTests.Factory.Day); Service = new(Db, GitHub.Object, days.Object); }
        public void Dispose() => Db.Dispose();
    }
    private static Mock<IGitHubRepositoryClient> GitHubMock()
    {
        var mock = new Mock<IGitHubRepositoryClient>(); mock.Setup(g => g.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new GitHubRepoInfoDto { Name = "Sample", Description = "GitHub description", Language = "Rust", Stars = 42, HtmlUrl = "https://github.com/owner/sample" }); return mock;
    }
    [Fact]
    public async Task SkillReviewDeduplicatesWithinBatchAcrossRetriesAndPreservesExistingProficiency()
    {
        using var f = new Fixture(); var original = await f.Service.SaveSkillAsync(f.User, null, new() { Name = "Rust", Proficiency = "Advanced" }, default);
        var items = new List<ExtractedSkillDto> { new() { Name = " rust " }, new() { Name = "C++" }, new() { Name = " c++ " } };
        await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => f.Service.SaveExtractedAsync(f.User, items, default)));
        Assert.Equal(2, f.Db.Skills.Count()); Assert.Equal("Advanced", f.Db.Skills.Single(s => s.Id == original!.Id).Proficiency);
        var replay = await f.Service.SaveSkillAsync(f.User, null, new() { Name = " RUST " }, default); Assert.Equal(original!.Id, replay!.Id);
        Assert.Null(await f.Service.SaveSkillAsync(Guid.NewGuid(), original.Id, new() { Name = "Foreign" }, default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.SaveExtractedAsync(f.User, [new() { Name = "Valid" }, new() { Name = "" }], default)); Assert.Equal(2, f.Db.Skills.Count());
    }
    [Fact]
    public async Task ConcurrentGitHubImportsReuseExistingExperienceAndKeepCuratedFields()
    {
        using var f = new Fixture(); var existing = new Project { UserId = f.User, Name = "Curated name", Description = "My detailed impact", Technologies = "C++, C", Link = "https://github.com/Owner/Sample.git", DeployedUrl = "https://example.invalid", IsFeatured = true };
        f.Db.Projects.Add(existing); await f.Db.SaveChangesAsync();
        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => f.Service.ImportAsync(f.User, "https://github.com/owner/sample/", default)));
        Assert.Single(f.Db.Projects); Assert.All(results, r => Assert.Equal(existing.Id, r.Id)); Assert.Equal("My detailed impact", existing.Description); Assert.Equal("C++, C", existing.Technologies); Assert.True(existing.IsFeatured); Assert.True(existing.IsPortfolioProject);
        await f.Service.SyncAsync(f.User, existing.Id, default); Assert.Equal("My detailed impact", existing.Description); Assert.Equal(42, existing.GitHubStars);
        var foreign = await f.Service.ImportAsync(Guid.NewGuid(), "https://github.com/owner/sample", default); Assert.NotEqual(existing.Id, foreign.Id);
    }
    [Fact]
    public async Task GitHubFailureDoesNotCreateHalfImportedProject()
    {
        using var f = new Fixture(); f.GitHub.Setup(g => g.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("Synthetic GitHub failure"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.ImportAsync(f.User, "https://github.com/owner/sample", default)); Assert.Empty(f.Db.Projects);
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.ImportAsync(f.User, "https://github.com.evil.invalid/owner/sample", default));
        f.GitHub.Verify(g => g.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }
    [Fact]
    public async Task DatesAndCertificateStatusRespectPartialDatesAndLocalDay()
    {
        using var f = new Fixture();
        var certificate = await f.Service.SaveCertificateAsync(f.User, null, new() { Name = "Cloud certificate", DateObtained = "2025-10", Expiry = "2026-10" }, default); Assert.Equal("expiring_soon", certificate!.ExpiryStatus);
        await f.Service.SaveCertificateAsync(f.User, null, new() { Name = "Old", Expiry = "2026-09" }, default); Assert.Contains(await f.Service.CertificatesAsync(f.User, default), c => c.ExpiryStatus == "expired");
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.SaveCertificateAsync(f.User, null, new() { Name = "Invalid", DateObtained = "2026-10", Expiry = "2026-09" }, default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.SaveWorkAsync(f.User, null, new() { Company = "A", Title = "B", StartDate = "2026-02-30" }, default));
        var work = await f.Service.SaveWorkAsync(f.User, null, new() { Company = " A ", Title = "Engineer", IsCurrent = true, StartDate = "2026", EndDate = "2025" }, default); Assert.Null(work!.EndDate); Assert.Equal("A", work.Company);
        Assert.Null(await f.Service.SaveWorkAsync(Guid.NewGuid(), work.Id, new() { Company = "A", Title = "B" }, default));
        Assert.Equal("valid", ProfessionalRecordRules.Expiry("2027", new(2026,10,14))); Assert.Equal("unknown", ProfessionalRecordRules.Expiry("legacy date", new(2026,10,14)));
    }
    [Fact]
    public async Task PortfolioWritesAreOwnedAndUrlsAreValidatedBeforePersistence()
    {
        using var f = new Fixture(); var project = await f.Service.SaveProjectAsync(f.User, null, new() { Name = "Original" }, default);
        Assert.Null(await f.Service.SavePortfolioAsync(Guid.NewGuid(), project!.Id, new() { Name = "Foreign" }, default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.SavePortfolioAsync(f.User, project!.Id, new() { Name = "Changed", DeployedUrl = "javascript:alert(1)" }, default));
        Assert.Equal("Original", (await f.Db.Projects.AsNoTracking().SingleAsync()).Name);
    }
    [Fact]
    public async Task ApiSavesPortfolioAtomicallyAndReturnsSafeValidationErrors()
    {
        using var app = new Factory(); using var client = app.CreateClient();
        var response = await client.PostAsJsonAsync("/api/experience/projects", new { name = "Original", description = "Original description" }); var p = await response.Content.ReadFromJsonAsync<ProjectDto>(); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        response = await client.PutAsJsonAsync($"/api/portfolio/projects/{p!.Id}/details", new { name = "Changed", description = "Changed", isPortfolioProject = true, deployedUrl = "javascript:alert(1)" }); Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var projects = await client.GetFromJsonAsync<List<ProjectDto>>("/api/experience/projects"); Assert.Equal("Original", projects![0].Name); Assert.Equal("Original description", projects[0].Description);
        response = await client.PutAsJsonAsync($"/api/portfolio/projects/{p.Id}/details", new { name = "Ready", description = "Curated", isPortfolioProject = true, gitHubRepoUrl = "https://github.com/owner/sample", deployedUrl = "https://example.invalid" }); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Ready", await client.GetStringAsync("/api/portfolio"));
        response = await client.PostAsJsonAsync("/api/portfolio/import", new { repoUrl = "https://github.com/OWNER/SAMPLE.git" }); var imported = await response.Content.ReadFromJsonAsync<PortfolioProjectDto>(); Assert.Equal(p.Id, imported!.Id); Assert.Equal("Curated", imported.Description);
        await client.PostAsJsonAsync("/api/skills/save-extracted", new[] { new { name = "Rust", category = "Programming Language", proficiency = "Intermediate" }, new { name = " rust ", category = "Tool", proficiency = "Beginner" } }); var skills = await client.GetFromJsonAsync<List<SkillDto>>("/api/skills"); Assert.Single(skills!);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/skills/certificates", new { name = "Invalid", dateObtained = "2026-10", expiry = "2025" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/experience/work", new { company = "", title = "" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync($"/api/portfolio/projects/{Guid.NewGuid()}/details", new { name = "Missing" })).StatusCode);
    }
    private class Factory : HabitApiTests.Factory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder) { base.ConfigureWebHost(builder); builder.ConfigureServices(s => { s.RemoveAll<IGitHubRepositoryClient>(); s.AddSingleton(GitHubMock().Object); }); }
    }
}
