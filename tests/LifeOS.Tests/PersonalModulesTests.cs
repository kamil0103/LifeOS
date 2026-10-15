using System.Net;
using System.Net.Http.Json;
using LifeOS.Application.DTOs.Personal;
using LifeOS.Application.DTOs.Bible;
using LifeOS.Application.DTOs.Analytics;
using LifeOS.Application.Interfaces;
using LifeOS.Domain.Entities;
using LifeOS.Infrastructure.Data;
using LifeOS.Infrastructure.Personal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;
namespace LifeOS.Tests;
public class PersonalModulesTests
{
    private sealed class Fixture : IDisposable
    {
        public readonly Guid User = Guid.NewGuid();
        public readonly AppDbContext Db = new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        public readonly Mock<ILocalDayService> Days = new();
        public Fixture() { Days.Setup(d => d.TodayAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(HabitApiTests.Factory.Day); }
        public void Dispose() => Db.Dispose();
    }
    [Fact]
    public void PostgreSqlModelMatchesTheMigrationSnapshot()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql("Host=localhost;Database=synthetic;Username=synthetic").Options);
        Assert.False(db.Database.HasPendingModelChanges());
    }
    [Fact]
    public async Task AnalyticsIncludesZeroDaysAndLocalBoundsAndDeduplicatesHabitDays()
    {
        using var f = new Fixture(); var day = HabitApiTests.Factory.Day; var habit = new Habit { UserId = f.User, Name = "Read", IsActive = true }; var foreign = new Habit { UserId = Guid.NewGuid(), Name = "PRIVATE" };
        f.Db.Habits.AddRange(habit, foreign); f.Db.HabitCompletions.AddRange(new() { HabitId = habit.Id, CompletedAt = day.Start.AddHours(1) }, new() { HabitId = habit.Id, CompletedAt = day.Start.AddHours(2) }, new() { HabitId = foreign.Id, CompletedAt = day.Start.AddHours(1) });
        f.Db.XpTransactions.AddRange(new() { UserId = f.User, Amount = 10, Source = "habit", CreatedAt = day.Start.AddMinutes(-1) }, new() { UserId = f.User, Amount = 20, Source = "coding", CreatedAt = day.End.AddMinutes(-1) }, new() { UserId = f.User, Amount = 900, CreatedAt = day.End }, new() { UserId = Guid.NewGuid(), Amount = 800, CreatedAt = day.Start }); await f.Db.SaveChangesAsync();
        var service = new AnalyticsService(f.Db, f.Days.Object); var xp = await service.XpAsync(f.User, 7, default); Assert.Equal(7, xp.Count); Assert.Equal(30, xp.Sum(d => d.Xp)); Assert.Equal(5, xp.Count(d => d.Xp == 0)); Assert.Equal(20, xp[^1].Xp);
        var heat = await service.HabitsAsync(f.User, 7, default); Assert.Equal(7, heat.Count); Assert.Equal(1, heat[^1].Count); Assert.Single(heat[^1].Habits); Assert.DoesNotContain(heat.SelectMany(h => h.Habits), h => h == "PRIVATE");
    }
    [Fact]
    public async Task CodingAnalyticsDistinguishesProblemsFromAttemptsAndFunnelIncludesWithdrawnAndSavedJobs()
    {
        using var f = new Fixture(); var day = HabitApiTests.Factory.Day; var p = new CodingProblem { UserId = f.User, Title = "One problem" }; f.Db.CodingProblems.Add(p);
        f.Db.ProblemAttempts.AddRange(new() { ProblemId = p.Id, SolvedAt = day.Offset(-1).Start }, new() { ProblemId = p.Id, SolvedAt = day.Start });
        var tracked = new Job { UserId = f.User, Title = "Tracked" }; f.Db.Jobs.AddRange(tracked, new() { UserId = f.User, Title = "Untracked" }); f.Db.JobApplications.Add(new() { UserId = f.User, JobId = tracked.Id, Status = "withdrawn" }); await f.Db.SaveChangesAsync();
        var service = new AnalyticsService(f.Db, f.Days.Object); var coding = await service.CodingAsync(f.User, default); Assert.Equal(1, coding.TotalProblems); Assert.Equal(2, coding.TotalAttempts); Assert.Equal(2, coding.Current); Assert.Equal(2, coding.Longest);
        var funnel = await service.FunnelAsync(f.User, default); Assert.Equal(1, funnel.Saved); Assert.Equal(1, funnel.Withdrawn); Assert.Equal(1, funnel.Total);
    }
    [Fact]
    public async Task JournalDatesUseTheUsersZoneIncludingDstAndRetriesDoNotDuplicate()
    {
        using var f = new Fixture(); var service = new PersonalRecordService(f.Db, f.Days.Object); var request = new CreateJournalEntryRequest { RequestId = Guid.NewGuid(), Title = "Reflection", Content = "Private text", Date = "2026-11-02" };
        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => service.SaveJournalAsync(f.User, null, request, default))); Assert.Single(f.Db.JournalEntries); Assert.All(results, r => Assert.Equal("2026-11-02", r!.LocalDate)); Assert.Equal(DateTimeOffset.Parse("2026-11-02T08:00:00Z"), results[0]!.EntryDate);
        Assert.Null(await service.SaveJournalAsync(Guid.NewGuid(), results[0]!.Id, new() { Title = "Foreign" }, default));
        await Assert.ThrowsAsync<RecordConflictException>(() => service.SaveJournalAsync(f.User, results[0]!.Id, new() { Title = "Stale", ExpectedUpdatedAt = results[0]!.UpdatedAt.AddSeconds(-10) }, default));
        Assert.Equal("Reflection", f.Db.JournalEntries.Single().Title);
        f.Days.Setup(d => d.TodayAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(LifeOS.Application.Services.LocalDay.At(HabitApiTests.Factory.Day.Start, "Pacific/Honolulu"));
        Assert.Equal("2026-11-02", (await service.JournalAsync(f.User, default))[0].LocalDate);
        f.Db.JournalEntries.Add(new() { UserId = f.User, Title = "Legacy date label", EntryDate = DateTimeOffset.Parse("2026-10-14T00:00:00Z") }); await f.Db.SaveChangesAsync();
        Assert.Equal("2026-10-14", (await service.JournalAsync(f.User, default)).Single(e => e.Title == "Legacy date label").LocalDate);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveJournalAsync(f.User, null, new() { Title = "Bad date", Date = "2026-02-30" }, default));
    }
    [Fact]
    public async Task RecipeRetriesAndAtomicReplacementPreserveOrderAndFixTemperatureScaling()
    {
        using var f = new Fixture(); var service = new PersonalRecordService(f.Db, f.Days.Object);
        var request = new SaveRecipeRequest { RequestId = Guid.NewGuid(), Name = "Soup", BaseServings = 2, Ingredients = [new() { Name = "Water", Quantity = 2, Unit = "cup" }], Steps = [new() { Text = "Heat", StepType = "cook", Variables = [new() { Name = "temperature", Value = 350, Unit = "°F", ScalingMode = "linear" }] }] };
        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => service.SaveRecipeAsync(f.User, null, request, default))); Assert.Single(f.Db.Recipes); Assert.Single(f.Db.RecipeIngredients); Assert.Single(results[0]!.Ingredients); Assert.Equal("none", results[0]!.Steps[0].Variables[0].ScalingMode);
        var update = new SaveRecipeRequest { Name = "New soup", BaseServings = 4, ExpectedUpdatedAt = results[0]!.UpdatedAt, Ingredients = [new() { Name = "Rice", Quantity = 1 }, new() { Name = "Water", Quantity = 3 }], Steps = [new() { Text = "Mix", StepType = "prep" }, new() { Text = "Cook", StepType = "cook" }] };
        var changed = await service.SaveRecipeAsync(f.User, results[0]!.Id, update, default); Assert.Equal(2, changed!.Ingredients.Count); Assert.Equal(new[] { "Rice", "Water" }, changed.Ingredients.Select(i => i.Name)); Assert.Equal(new[] { 1, 2 }, changed.Steps.Select(s => s.StepNumber)); Assert.Equal(2, f.Db.RecipeIngredients.Count()); Assert.Equal(2, f.Db.RecipeInstructionSteps.Count());
        update.ExpectedUpdatedAt = changed.UpdatedAt.AddSeconds(-10); await Assert.ThrowsAsync<RecordConflictException>(() => service.SaveRecipeAsync(f.User, changed.Id, update, default));
        Assert.False(await service.DeleteRecipeAsync(Guid.NewGuid(), changed.Id, default));
    }
    [Fact]
    public async Task ConcurrentBookmarkSavesHaveValidReferencesAndKeepOwnership()
    {
        using var f = new Fixture(); var book = new BibleBook { Name = "Test book", ChapterCount = 2, BookOrder = 1 }; var verse = new BibleVerse { BookId = book.Id, Chapter = 1, VerseNumber = 1, Text = "Test verse" }; f.Db.BibleBooks.Add(book); f.Db.BibleVerses.Add(verse); await f.Db.SaveChangesAsync(); var service = new BibleReaderService(f.Db, f.Days.Object);
        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => service.BookmarkAsync(f.User, new() { VerseId = verse.Id, Note = "My note", Color = "blue" }, default))); Assert.Single(f.Db.Bookmarks); Assert.All(results, b => Assert.Equal("Test book 1:1", b.Reference)); Assert.False(await service.DeleteBookmarkAsync(Guid.NewGuid(), results[0].Id, default));
        var a = await service.DailyAsync(f.User, false, default); var b = await service.DailyAsync(f.User, false, default); Assert.Equal(a!.Id, b!.Id);
    }
    [Fact]
    public async Task ApiReportsDailyGridsAndUsesLocalJournalDatesAndSafeRecipeValidation()
    {
        using var app = new HabitApiTests.Factory(); using var client = app.CreateClient();
        var report = await client.GetFromJsonAsync<AnalyticsReportDto>("/api/analytics/report?days=7"); Assert.Equal(7, report!.HabitHeatmap.Count); Assert.Equal(7, report.XpTrends.Count); Assert.Equal("America/Los_Angeles", report.TimeZone);
        var requestId = Guid.NewGuid(); var response = await client.PostAsJsonAsync("/api/journal", new { title = "My reflection", content = "PRIVATE_BODY", date = "2026-11-02", requestId }); Assert.Equal(HttpStatusCode.OK, response.StatusCode); var journal = await response.Content.ReadFromJsonAsync<JournalEntryDto>(); Assert.Equal("2026-11-02", journal!.LocalDate);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/journal", new { title = "My reflection", content = "PRIVATE_BODY", date = "2026-11-02", requestId })).StatusCode); Assert.Single((await client.GetFromJsonAsync<List<JournalEntryDto>>("/api/journal"))!);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync("/api/journal/" + journal.Id, new { title = "Stale", expectedUpdatedAt = journal.UpdatedAt.AddDays(-1) })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/recipes", new { name = "Invalid", baseServings = 0 })).StatusCode);
        response = await client.PostAsJsonAsync("/api/recipes", new { name = "Test", baseServings = 2, ingredients = new[] { new { name = "Water", quantity = 1 } } }); Assert.Equal(HttpStatusCode.OK, response.StatusCode); var recipe = await response.Content.ReadFromJsonAsync<RecipeDto>(); Assert.Single(recipe!.Ingredients);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync("/api/recipes/" + Guid.NewGuid())).StatusCode);
    }
    [Fact]
    public async Task ApiBookmarkCreationWorksAndLegacyDuplicatesDoNotBreakChapterLoading()
    {
        using var app = new HabitApiTests.Factory(); using var client = app.CreateClient(); Guid bookId, verseId;
        using (var scope = app.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var book = new BibleBook { Name = "Test", ChapterCount = 1, BookOrder = 1 }; var verse = new BibleVerse { BookId = book.Id, Chapter = 1, VerseNumber = 1, Text = "Test text" }; db.BibleBooks.Add(book); db.BibleVerses.Add(verse); await db.SaveChangesAsync(); bookId = book.Id; verseId = verse.Id; }
        var response = await client.PostAsJsonAsync("/api/bible/bookmarks", new { verseId, note = "Owned note", color = "purple" }); Assert.Equal(HttpStatusCode.OK, response.StatusCode); var bookmark = await response.Content.ReadFromJsonAsync<BookmarkDto>(); Assert.Equal(bookId, bookmark!.BookId);
        using (var scope = app.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); db.Bookmarks.Add(new() { UserId = HabitApiTests.TestAuth.UserId, VerseId = verseId, Note = "Legacy duplicate" }); await db.SaveChangesAsync(); }
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/bible/books/{bookId}/chapters/1")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"/api/bible/books/{bookId}/chapters/2")).StatusCode);
    }
}
