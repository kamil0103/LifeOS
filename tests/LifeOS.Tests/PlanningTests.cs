using System.Net;
using System.Net.Http.Json;
using LifeOS.Application.DTOs.Planning;
using LifeOS.Application.Interfaces;
using LifeOS.Domain.Entities;
using LifeOS.Infrastructure.Data;
using LifeOS.Infrastructure.Planning;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Moq;
using LifeOS.Application.Services;
using LifeOS.Application.DTOs.Calendar;
using LifeOS.Infrastructure.Calendar;
namespace LifeOS.Tests;
public class PlanningTests
{
    [Theory]
    [InlineData(0,1)][InlineData(1,1)][InlineData(2,3)][InlineData(3,7)][InlineData(4,14)][InlineData(5,30)]
    public void ReviewsSpaceOutWithAttempts(int attempts,int days)=>Assert.Equal(days,PlanningService.ReviewDays(attempts));
    [Fact]
    public async Task NamespacedApplicationRouteSupportsLifecycleAndLegacyAlias()
    {
        using var app=new HabitApiTests.Factory();using var client=app.CreateClient();Guid jobId;
        using(var scope=app.Services.CreateScope()){var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();var job=new Job{UserId=HabitApiTests.TestAuth.UserId,Title="Test role",Company="Example"};db.Jobs.Add(job);await db.SaveChangesAsync();jobId=job.Id;}
        var created=await client.PostAsJsonAsync("/api/job-applications",new{jobId});Assert.Equal(HttpStatusCode.OK,created.StatusCode);
        var body=await created.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();var id=body.GetProperty("id").GetGuid();
        Assert.Contains("Test role",await client.GetStringAsync("/api/job-applications"));
        Assert.Equal(HttpStatusCode.OK,(await client.PutAsJsonAsync($"/api/job-applications/{id}/status",new{status="interview"})).StatusCode);
        Assert.Contains("interview",await client.GetStringAsync($"/api/job-applications/{id}"));
        Assert.Contains("Test role",await client.GetStringAsync("/api/applications"));
        Assert.Equal(HttpStatusCode.NoContent,(await client.DeleteAsync($"/api/job-applications/{id}")).StatusCode);
    }
    [Fact]
    public async Task HabitSchedulingIsIdempotentForOneDayAndAvailableAgainNextDay()
    {
        using var db=new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var user=Guid.NewGuid();var day=LocalDay.At(DateTimeOffset.UtcNow,"UTC");var habit=new Habit{UserId=user,Name="Walk",IsActive=true};db.Habits.Add(habit);await db.SaveChangesAsync();
        var days=new Mock<ILocalDayService>();days.Setup(d=>d.TodayAsync(user,It.IsAny<CancellationToken>())).Returns(()=>Task.FromResult(day));
        var google=new Mock<IGoogleCalendarService>();google.Setup(g=>g.EventsAsync(user,It.IsAny<DateTimeOffset>(),It.IsAny<DateTimeOffset>(),false,It.IsAny<CancellationToken>())).ReturnsAsync(new List<GoogleEventDto>());
        var service=new PlanningService(db,days.Object,new CalendarService(db),google.Object);
        var first=await service.ScheduleAsync(user,new("habit:"+habit.Id,day.Start.AddHours(10),day.Start.AddHours(11),"local"),default);
        var again=await service.ScheduleAsync(user,new("habit:"+habit.Id,day.Start.AddHours(12),day.Start.AddHours(13),"local"),default);
        Assert.Equal(first.EventId,again.EventId);Assert.True(again.AlreadyScheduled);
        day=day.Offset(1);var next=await service.ScheduleAsync(user,new("habit:"+habit.Id,day.Start.AddHours(10),day.Start.AddHours(11),"local"),default);Assert.NotEqual(first.EventId,next.EventId);Assert.Equal(2,await db.CalendarEvents.CountAsync());
    }
    [Fact]
    public async Task TodayFiltersOwnershipAndSortsOverdueBeforeHabitsWithGoogleFallback()
    {
        using var db=new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var user=Guid.NewGuid();var day=LocalDay.At(DateTimeOffset.UtcNow,"UTC");
        db.Habits.AddRange(new Habit{UserId=user,Name="Owned",IsActive=true},new Habit{UserId=Guid.NewGuid(),Name="Private",IsActive=true});
        var job=new Job{UserId=user,Title="Engineer",Company="Example"};db.Jobs.Add(job);
        db.JobApplications.Add(new JobApplication{UserId=user,JobId=job.Id,FollowUpDate=day.Start.AddDays(-1).UtcDateTime});await db.SaveChangesAsync();
        var days=new Mock<ILocalDayService>();days.Setup(d=>d.TodayAsync(user,It.IsAny<CancellationToken>())).ReturnsAsync(day);
        var google=new Mock<IGoogleCalendarService>();google.Setup(g=>g.EventsAsync(user,day.Start,day.End,false,It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException());
        var plan=await new PlanningService(db,days.Object,new CalendarService(db),google.Object).TodayAsync(user,default);
        Assert.Equal("followup",plan.Actions.First().Kind);Assert.Contains(plan.Actions,a=>a.Title=="Owned");Assert.DoesNotContain(plan.Actions,a=>a.Title=="Private");Assert.NotNull(plan.GoogleWarning);
    }
    [Fact]
    public async Task FollowUpIsOwnedAndStaysLinkedToCalendar()
    {
        using var app=new HabitApiTests.Factory();using var client=app.CreateClient();Guid id;
        using(var scope=app.Services.CreateScope()){
            var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();var job=new Job{UserId=HabitApiTests.TestAuth.UserId,Title="Engineer",Company="Example"};db.Jobs.Add(job);
            var application=new JobApplication{UserId=job.UserId,JobId=job.Id};db.JobApplications.Add(application);id=application.Id;await db.SaveChangesAsync();
        }
        var due=HabitApiTests.Factory.Day.Start.AddHours(10);
        Assert.Equal(HttpStatusCode.NoContent,(await client.PutAsJsonAsync($"/api/planning/applications/{id}/follow-up",new{dueAt=due,notes="Follow-up scheduled"})).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,(await client.PutAsJsonAsync($"/api/planning/applications/{id}/follow-up",new{dueAt=due.AddHours(1),endAt=due.AddHours(3)})).StatusCode);
        Guid eventId;
        using(var scope=app.Services.CreateScope()) { var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();var item=Assert.Single(await db.CalendarEvents.Where(e=>e.ApplicationId==id).ToListAsync());eventId=item.Id;Assert.Equal(due.AddHours(1),item.StartTime);Assert.Equal(due.AddHours(3),item.EndTime); }
        Assert.Equal(HttpStatusCode.OK,(await client.PutAsJsonAsync($"/api/calendar/events/{eventId}",new{title="Follow up",startTime=due.AddHours(2),endTime=due.AddHours(3),eventType="general"})).StatusCode);
        using(var scope=app.Services.CreateScope()) { var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();Assert.Equal(due.AddHours(2).UtcDateTime,(await db.JobApplications.FindAsync(id))!.FollowUpDate);Assert.Equal("followup",(await db.CalendarEvents.FindAsync(eventId))!.EventType); }
        Assert.Equal(HttpStatusCode.NoContent,(await client.DeleteAsync($"/api/calendar/events/{eventId}")).StatusCode);
        using(var scope=app.Services.CreateScope()) { var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();Assert.Null((await db.JobApplications.FindAsync(id))!.FollowUpDate); }
        Assert.Equal(HttpStatusCode.NotFound,(await client.PutAsJsonAsync($"/api/planning/applications/{Guid.NewGuid()}/follow-up",new{dueAt=due,notes="Follow-up scheduled"})).StatusCode);
    }
    [Fact]
    public async Task ForeignDocumentsCannotBeAttached()
    {
        using var app=new HabitApiTests.Factory();using var client=app.CreateClient();Guid id,resumeId;
        using(var scope=app.Services.CreateScope()) { var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();var job=new Job{UserId=HabitApiTests.TestAuth.UserId,Title="Role",Company="Example"};db.Jobs.Add(job);var application=new JobApplication{UserId=job.UserId,JobId=job.Id};db.JobApplications.Add(application);id=application.Id;var resume=new ResumeVersion{UserId=Guid.NewGuid(),Title="Private"};db.ResumeVersions.Add(resume);resumeId=resume.Id;await db.SaveChangesAsync(); }
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PutAsJsonAsync($"/api/planning/applications/{id}/details",new{resumeVersionId=resumeId})).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PutAsJsonAsync($"/api/applications/{id}/status",new{status="invalid"})).StatusCode);
    }
    [Fact]
    public async Task RemindersDeduplicateSnoozeDismissAndRespectOwnership()
    {
        using var db=new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var service=new ReminderService(db);var user=Guid.NewGuid();var plan=new DailyPlanDto{TimeZone="UTC",Actions=[new("followup:test","followup",null,"Follow up","Reason",0,false,DateTimeOffset.UtcNow.AddHours(-1),"/jobs/applications",true)]};
        await service.RemindAsync(user,plan,default);await service.RemindAsync(user,plan,default);var item=Assert.Single(await service.ListAsync(user,false,1,default));
        Assert.False(await service.ArchiveAsync(Guid.NewGuid(),item.Id,default));
        Assert.True(await service.SnoozeAsync(user,item.Id,60,default));Assert.Equal(0,await service.CountAsync(user,default));await service.RemindAsync(user,plan,default);Assert.Single(db.Notifications);
        Assert.True(await service.ArchiveAsync(user,item.Id,default));await service.RemindAsync(user,plan,default);Assert.Single(db.Notifications);Assert.Empty(await service.ListAsync(user,false,1,default));
        plan.Actions=[new("followup:second","followup",null,"Second","Reason",0,false,DateTimeOffset.UtcNow.AddHours(-1),"/jobs/applications",true)];await service.RemindAsync(user,plan,default);Assert.Equal(1,await service.CountAsync(user,default));
        await service.SavePreferencesAsync(user,new(false,false,false,30),default);await service.RemindAsync(user,plan,default);Assert.Equal(0,await service.CountAsync(user,default));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>service.SavePreferencesAsync(user,new(true,true,true,1441),default));
    }
}
