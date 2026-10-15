using System.Net;
using System.Net.Http.Json;
using LifeOS.Application.DTOs.Habits;
using LifeOS.Application.Interfaces;
using LifeOS.Application.Services;
using LifeOS.Domain.Entities;
using LifeOS.Infrastructure.Data;
using LifeOS.Infrastructure.Habits;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
namespace LifeOS.Tests;
public class HabitWorkflowTests
{
    private class Fixture : IDisposable
    {
        public AppDbContext Db=new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        public Guid User=Guid.NewGuid(); public LocalDay Day=LocalDay.At(DateTimeOffset.Parse("2026-10-16T02:00:00Z"),"America/Los_Angeles");
        public HabitService Service;
        public Fixture(){var days=new Mock<ILocalDayService>();days.Setup(d=>d.TodayAsync(It.IsAny<Guid>(),It.IsAny<CancellationToken>())).ReturnsAsync(()=>Day);var clock=new Mock<TimeProvider>();clock.Setup(c=>c.GetUtcNow()).Returns(()=>Day.Start.AddHours(12));Service=new(Db,days.Object,clock.Object);}
        public void Dispose()=>Db.Dispose();
    }
    [Fact] public async Task PartialProgressEarnsXpOnlyOnceAtTheTargetAndArchiveKeepsHistory()
    {
        using var f=new Fixture();var h=await f.Service.SaveAsync(f.User,null,new(){Name="Read",Category="Reading",TargetValue=10,Unit="pages"},default);
        var partial=await f.Service.ProgressAsync(f.User,h!.Id,new(){Value=4},false,default);Assert.False(partial!.IsCompleted);Assert.Equal(0,partial.XpEarned);Assert.Empty(f.Db.XpTransactions);
        Assert.False((await f.Service.ListAsync(f.User,false,true,default)).Single().IsCompleted);
        var done=await f.Service.ProgressAsync(f.User,h.Id,new(){Value=10},false,default);Assert.True(done!.IsCompleted);Assert.Equal(10,done.XpEarned);Assert.Equal(1,done.CurrentStreak);
        var retry=await f.Service.ProgressAsync(f.User,h.Id,new(){Value=10},false,default);Assert.Equal(0,retry!.XpEarned);Assert.Single(f.Db.XpTransactions);Assert.Single(f.Db.HabitCompletions);
        await Assert.ThrowsAsync<RecordConflictException>(()=>f.Service.ProgressAsync(f.User,h.Id,new(){Value=2},false,default));
        Assert.True(await f.Service.SetActiveAsync(f.User,h.Id,false,default));Assert.Empty(await f.Service.ListAsync(f.User,false,false,default));Assert.Single(await f.Service.ListAsync(f.User,true,false,default));Assert.Null(await f.Service.ProgressAsync(f.User,h.Id,new(){Value=10},false,default));
        Assert.Single((await f.Service.HistoryAsync(f.User,h.Id,30,default))!);await f.Service.SetActiveAsync(f.User,h.Id,true,default);Assert.True((await f.Service.ListAsync(f.User,false,true,default)).Single().IsCompleted);Assert.Single(f.Db.XpTransactions);
    }
    [Fact] public async Task SchedulesUseLocalDaysAndStreaksSkipOffDays()
    {
        using var f=new Fixture();f.Day=LocalDay.At(DateTimeOffset.Parse("2026-10-17T02:00:00Z"),"America/Los_Angeles"); // Friday locally
        var h=await f.Service.SaveAsync(f.User,null,new(){Name="Weekdays",Category="Coding",ScheduleDaysMask=62},default);
        Assert.Single(await f.Service.ListAsync(f.User,false,true,default));await f.Service.ProgressAsync(f.User,h!.Id,null,true,default);
        f.Day=f.Day.Offset(1);Assert.Empty(await f.Service.ListAsync(f.User,false,true,default));await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Service.ProgressAsync(f.User,h.Id,null,true,default));
        f.Day=f.Day.Offset(2);Assert.Equal(DayOfWeek.Monday,f.Day.Date.DayOfWeek);Assert.Equal(2,(await f.Service.ProgressAsync(f.User,h.Id,null,true,default))!.CurrentStreak);
        f.Day=f.Day.Offset(2);Assert.Equal(0,(await f.Service.ListAsync(f.User,false,true,default)).Single().CurrentStreak);Assert.Equal(1,(await f.Service.ProgressAsync(f.User,h.Id,null,true,default))!.CurrentStreak);
    }
    [Fact] public async Task ValidationsOwnershipAndStaleEditsAreEnforced()
    {
        using var f=new Fixture();await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Service.SaveAsync(f.User,null,new(){Name="Bad",ScheduleDaysMask=0},default));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Service.SaveAsync(f.User,null,new(){Name="Bad",TargetValue=-1},default));
        var h=await f.Service.SaveAsync(f.User,null,new(){Name="Owned",TargetValue=5},default);var old=h!.UpdatedAt;
        Assert.Null(await f.Service.SaveAsync(Guid.NewGuid(),h.Id,new(){Name="Foreign"},default));Assert.Null(await f.Service.HistoryAsync(Guid.NewGuid(),h.Id,30,default));Assert.False(await f.Service.SetActiveAsync(Guid.NewGuid(),h.Id,false,default));
        await f.Service.SaveAsync(f.User,h.Id,new(){Name="Edited",ExpectedUpdatedAt=old,TargetValue=5},default);
        await Assert.ThrowsAsync<RecordConflictException>(()=>f.Service.SaveAsync(f.User,h.Id,new(){Name="Stale",ExpectedUpdatedAt=old},default));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Service.ProgressAsync(f.User,h.Id,null,true,default));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Service.ProgressAsync(f.User,h.Id,new(){Value=-1},false,default));Assert.Empty(f.Db.XpTransactions);
    }
    [Fact] public void ScheduleBoundariesUseProfileTimezoneAcrossDst()
    {
        var instant=DateTimeOffset.Parse("2026-11-02T01:00:00Z");var la=LocalDay.At(instant,"America/Los_Angeles");var tokyo=LocalDay.At(instant,"Asia/Tokyo");var habit=new Habit{ScheduleDaysMask=2};
        Assert.False(HabitSchedule.IsDue(habit,la));Assert.True(HabitSchedule.IsDue(habit,tokyo));Assert.Equal(TimeSpan.FromHours(25),la.End-la.Start);
    }
    [Fact] public async Task ApiSupportsPartialProgressArchiveRestoreAndDueOnlyViews()
    {
        using var app=new HabitApiTests.Factory();using var client=app.CreateClient();
        var response=await client.PostAsJsonAsync("/api/habits",new{Name="Numeric",category="Coding",targetValue=10,scheduleDaysMask=127});Assert.Equal(HttpStatusCode.OK,response.StatusCode);var h=await response.Content.ReadFromJsonAsync<HabitDto>();
        var partial=await (await client.PostAsJsonAsync($"/api/habits/{h!.Id}/progress",new{value=3})).Content.ReadFromJsonAsync<HabitCompletionResult>();Assert.False(partial!.IsCompleted);
        var plan=await client.GetStringAsync("/api/planning/today");Assert.Contains("Log today's total",plan);Assert.Contains("\"canComplete\":false",plan);
        Assert.Equal(HttpStatusCode.NoContent,(await client.DeleteAsync($"/api/habits/{h.Id}")).StatusCode);Assert.Empty((await client.GetFromJsonAsync<List<HabitDto>>("/api/habits/today"))!);Assert.Single((await client.GetFromJsonAsync<List<HabitDto>>("/api/habits?archived=true"))!);
        Assert.Equal(HttpStatusCode.NoContent,(await client.PostAsync($"/api/habits/{h.Id}/restore",null)).StatusCode);var done=await (await client.PostAsJsonAsync($"/api/habits/{h.Id}/progress",new{value=10})).Content.ReadFromJsonAsync<HabitCompletionResult>();Assert.True(done!.IsCompleted);Assert.Equal(25,done.XpEarned);
    }
}
