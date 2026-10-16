using System.Net;
using System.Net.Http.Json;
using LifeOS.Application.DTOs.Coding;
using LifeOS.Application.Interfaces;
using LifeOS.Application.Services;
using LifeOS.Domain.Entities;
using LifeOS.Infrastructure.Coding;
using LifeOS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;
namespace LifeOS.Tests;
public class CodingProgressTests
{
    private static AppDbContext Database() => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static Mock<ILocalDayService> Days() { var days = new Mock<ILocalDayService>(); days.Setup(d => d.TodayAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(LocalDay.At(DateTimeOffset.UtcNow, "America/Los_Angeles")); return days; }
    [Fact]
    public async Task RepeatedSolvesAndUnsolveKeepOneAttemptAndXp()
    {
        using var db = Database(); var user = Guid.NewGuid(); var service = new CodingProgressService(db, Days().Object);
        var p = await service.CreateAsync(user, new() { Title = " Two Sum " }, default); Assert.Equal("Two Sum", p.Title);
        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => service.RecordAsync(user, p.Id, new() { Notes = "Hash map, O(n)", TimeSpentMinutes = 15 }, false, default)));
        Assert.All(results, r => Assert.Equal(1, r!.AttemptCount));
        await service.UnsolveAsync(user, p.Id, default); await service.RecordAsync(user, p.Id, new(), false, default); await service.RecordAsync(user, p.Id, new(), true, default);
        Assert.Single(db.ProblemAttempts); Assert.Single(db.XpTransactions); Assert.Equal(15, db.XpTransactions.Single().Amount); Assert.Equal("Hash map, O(n)", db.ProblemAttempts.Single().Notes);
    }
    [Fact]
    public async Task ReviewRestoresSolvedStateAndOnlyAwardsReviewXp()
    {
        using var db = Database(); var user = Guid.NewGuid(); var service = new CodingProgressService(db, Days().Object);
        var p = new CodingProblem { UserId = user, Title = "Old hard problem", Difficulty = "hard" }; db.CodingProblems.Add(p); db.ProblemAttempts.Add(new() { ProblemId = p.Id, SolvedAt = DateTimeOffset.UtcNow.AddDays(-3), XpEarned = 55 }); await db.SaveChangesAsync();
        var result = await service.RecordAsync(user, p.Id, new() { SolutionLanguage = "Rust", Notes = "Reviewed ownership", TimeSpentMinutes = 20 }, true, default);
        Assert.True(result!.IsSolved); Assert.Equal(2, result.AttemptCount); Assert.Equal(5, db.XpTransactions.Single().Amount); Assert.Equal("Rust", result.SolutionLanguage);
    }
    [Fact]
    public async Task OwnershipAndValidationProtectRecords()
    {
        using var db = Database(); var user = Guid.NewGuid(); var service = new CodingProgressService(db, Days().Object); var p = await service.CreateAsync(user, new() { Title = "Private" }, default); var other = Guid.NewGuid();
        Assert.Null(await service.RecordAsync(other, p.Id, new(), false, default)); Assert.Null(await service.UnsolveAsync(other, p.Id, default)); Assert.False(await service.DeleteAsync(other, p.Id, default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RecordAsync(user, p.Id, new(), true, default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RecordAsync(user, p.Id, new() { TimeSpentMinutes = -5 }, false, default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(user, new() { Title = "A", Url = "javascript:alert(1)" }, default)); Assert.Empty(db.ProblemAttempts);
    }
    [Fact]
    public async Task ApiCountsAttemptsAndKeepsXpWhenUnsolved()
    {
        using var app = new HabitApiTests.Factory(); using var client = app.CreateClient(); Guid id;
        using (var scope = app.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var p = new CodingProblem { UserId = HabitApiTests.TestAuth.UserId, Title = "History", IsSolved = true, SolvedAt = HabitApiTests.Factory.Day.Start.AddHours(1) }; db.CodingProblems.Add(p); db.ProblemAttempts.Add(new() { ProblemId = p.Id, SolvedAt = p.SolvedAt.Value, Notes = "Two pointers", XpEarned = 15 }); await db.SaveChangesAsync(); id = p.Id; db.CodingProblems.Add(new() { UserId = Guid.NewGuid(), Title = "Private foreign problem" }); await db.SaveChangesAsync(); }
        var list = await client.GetFromJsonAsync<List<CodingProblemDto>>("/api/coding/problems"); Assert.Single(list!); Assert.Equal(1, list![0].AttemptCount); Assert.Contains("Two pointers", await client.GetStringAsync($"/api/coding/problems/{id}/attempts"));
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/coding/problems/{id}/unsolve", null)).StatusCode);
        var stats = await client.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/coding/stats"); Assert.Equal(15, stats.GetProperty("totalXpEarned").GetInt32());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/coding/problems/{id}/solve", new { timeSpentMinutes = 0 })).StatusCode); Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/coding/problems/{Guid.NewGuid()}/attempts")).StatusCode);
    }
}
