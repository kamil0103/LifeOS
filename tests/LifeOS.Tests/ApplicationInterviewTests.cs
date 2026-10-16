using System.Net;
using System.Net.Http.Json;
using LifeOS.Application.DTOs.Jobs;
using LifeOS.Application.Interfaces;
using LifeOS.Domain.Entities;
using LifeOS.Infrastructure.Calendar;
using LifeOS.Infrastructure.Career;
using LifeOS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;
namespace LifeOS.Tests;
public class ApplicationInterviewTests
{
    private sealed class Fixture : IDisposable
    {
        public readonly AppDbContext Db = new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        public readonly Guid User = Guid.NewGuid(); public readonly JobApplication App; public readonly ApplicationInterviewService Service;
        public Fixture() { var job = new Job { UserId = User, Company = "Devices", Title = "Firmware" }; App = new() { UserId = User, Job = job, JobId = job.Id, Status = "applied", FollowUpDate = DateTime.UtcNow.AddDays(5) }; Db.Jobs.Add(job); Db.JobApplications.Add(App); Db.SaveChanges(); var clock = new Mock<TimeProvider>(); clock.Setup(c => c.GetUtcNow()).Returns(DateTimeOffset.Parse("2026-11-01T00:00:00Z")); Service = new(Db, clock.Object); }
        public SaveInterviewRequest Request() => new() { RequestId = Guid.NewGuid(), Round = "Technical", Format = "video", StartTime = DateTimeOffset.Parse("2026-11-02T10:00:00-08:00"), EndTime = DateTimeOffset.Parse("2026-11-02T11:00:00-08:00"), MeetingUrl = "https://example.com/meeting", Preparation = "Interrupts" };
        public void Dispose() => Db.Dispose();
    }
    [Fact]
    public async Task RetrySafeCreationReschedulingAndCancellationKeepHistoryAndFollowUpSeparate()
    {
        using var f = new Fixture(); var r = f.Request(); var created = (await f.Service.SaveAsync(f.User, f.App.Id, null, r, default))!;
        var retry = await f.Service.SaveAsync(f.User, f.App.Id, null, r, default); Assert.Equal(created.Id, retry!.Id); Assert.Single(f.Db.ApplicationInterviews); Assert.Single(f.Db.CalendarEvents);
        Assert.Single(retry.History); Assert.Equal(TimeSpan.Zero, retry.StartTime.Offset); Assert.Equal("applied", f.App.Status); Assert.NotNull(f.App.FollowUpDate);
        r.ExpectedUpdatedAt = created.UpdatedAt; r.StartTime = r.StartTime.AddDays(1); r.EndTime = r.EndTime.AddDays(1); var moved = (await f.Service.SaveAsync(f.User, f.App.Id, created.Id, r, default))!;
        Assert.Equal(created.CalendarEventId, moved.CalendarEventId); Assert.Equal(2, moved.History.Count); Assert.Equal(created.StartTime, moved.History[0].StartTime); Assert.Equal(moved.StartTime, f.Db.CalendarEvents.Single().StartTime);
        r.ExpectedUpdatedAt = moved.UpdatedAt; r.Status = "cancelled"; r.Outcome = "Company postponed"; var cancelled = (await f.Service.SaveAsync(f.User, f.App.Id, moved.Id, r, default))!;
        Assert.Null(cancelled.CalendarEventId); Assert.Empty(f.Db.CalendarEvents); Assert.Equal(3, cancelled.History.Count); Assert.Equal("Company postponed", cancelled.History[^1].Outcome);
    }
    [Fact]
    public async Task CompletedInterviewsRemainOnCalendarAndHistoryCannotBeEditedThroughCalendar()
    {
        using var f = new Fixture(); var r = f.Request(); var interview = (await f.Service.SaveAsync(f.User, f.App.Id, null, r, default))!;
        var calendar = new CalendarService(f.Db);
        await Assert.ThrowsAsync<InvalidOperationException>(() => calendar.UpdateAsync(f.User, interview.CalendarEventId!.Value, new() { Title = "Bypass", StartTime = r.StartTime, EndTime = r.EndTime }, default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => calendar.DeleteAsync(f.User, interview.CalendarEventId!.Value, default));
        r.ExpectedUpdatedAt = interview.UpdatedAt; r.Status = "completed"; r.Outcome = "Good discussion"; var completed = (await f.Service.SaveAsync(f.User, f.App.Id, interview.Id, r, default))!;
        Assert.NotNull(completed.CalendarEventId); Assert.Equal("Good discussion", completed.Outcome); Assert.Equal("applied", f.App.Status);
    }
    [Fact]
    public async Task OwnershipValidationConflictsAndStaleEditsDoNotChangeTheRecord()
    {
        using var f = new Fixture(); var r = f.Request(); var n = (await f.Service.SaveAsync(f.User, f.App.Id, null, r, default))!;
        Assert.Null(await f.Service.ListAsync(Guid.NewGuid(), f.App.Id, default)); Assert.Null(await f.Service.SaveAsync(Guid.NewGuid(), f.App.Id, null, r, default));
        r.ExpectedUpdatedAt = n.UpdatedAt.AddDays(-1); await Assert.ThrowsAsync<RecordConflictException>(() => f.Service.SaveAsync(f.User, f.App.Id, n.Id, r, default));
        r.ExpectedUpdatedAt = n.UpdatedAt; r.MeetingUrl = "javascript:alert(1)"; await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.SaveAsync(f.User, f.App.Id, n.Id, r, default));
        var overlap = f.Request(); await Assert.ThrowsAsync<RecordConflictException>(() => f.Service.SaveAsync(f.User, f.App.Id, null, overlap, default));
        Assert.Single(f.Db.ApplicationInterviews); Assert.Single(f.Db.CalendarEvents); Assert.Single((await f.Service.ListAsync(f.User, f.App.Id, default))![0].History);
        var duplicate = f.Request(); duplicate.RequestId = n.Id; duplicate.EndTime = duplicate.StartTime; await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.SaveAsync(f.User, f.App.Id, null, duplicate, default));
    }
    [Fact]
    public async Task ReusingCreationIdentifierForDifferentDataFailsAndDeletingApplicationRemovesInterviews()
    {
        using var f = new Fixture(); var r = f.Request(); await f.Service.SaveAsync(f.User, f.App.Id, null, r, default); r.Round = "Different";
        await Assert.ThrowsAsync<RecordConflictException>(() => f.Service.SaveAsync(f.User, f.App.Id, null, r, default));
        Assert.False(await f.Service.DeleteApplicationAsync(Guid.NewGuid(), f.App.Id, default)); Assert.True(await f.Service.DeleteApplicationAsync(f.User, f.App.Id, default));
        Assert.Empty(f.Db.ApplicationInterviews); Assert.Empty(f.Db.CalendarEvents); Assert.Single(f.Db.Jobs);
    }
    [Fact]
    public async Task ApiCreatesOwnedInterviewsAndEnforcesRequiredVersionsAndValidation()
    {
        using var app = new HabitApiTests.Factory(); using var client = app.CreateClient(); Guid appId;
        using (var scope = app.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var job = new Job { UserId = HabitApiTests.TestAuth.UserId, Company = "Devices" }; var application = new JobApplication { UserId = job.UserId, Job = job }; db.JobApplications.Add(application); await db.SaveChangesAsync(); appId = application.Id; }
        var path = $"/api/job-applications/{appId}/interviews"; var r = new SaveInterviewRequest { RequestId = Guid.NewGuid(), Round = "Screen", StartTime = DateTimeOffset.UtcNow.AddDays(7), EndTime = DateTimeOffset.UtcNow.AddDays(7).AddHours(1) };
        var response = await client.PostAsJsonAsync(path, r); response.EnsureSuccessStatusCode(); var item = (await response.Content.ReadFromJsonAsync<ApplicationInterviewDto>())!;
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync(path + "/" + item.Id, r)).StatusCode);
        r.ExpectedUpdatedAt = item.UpdatedAt.AddDays(-1); Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync(path + "/" + item.Id, r)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/job-applications/{Guid.NewGuid()}/interviews")).StatusCode);
        r.ExpectedUpdatedAt = item.UpdatedAt; r.Round = new string('x', 151); Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync(path + "/" + item.Id, r)).StatusCode);
    }
}
