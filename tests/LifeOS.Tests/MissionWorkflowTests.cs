using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LifeOS.Application.DTOs.Calendar;
using LifeOS.Application.DTOs.Coach;
using LifeOS.Application.Interfaces;
using LifeOS.Application.Services;
using LifeOS.Domain.Entities;
using LifeOS.Infrastructure.Calendar;
using LifeOS.Infrastructure.Coach;
using LifeOS.Infrastructure.Data;
using LifeOS.Infrastructure.Planning;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.AspNetCore.Hosting;
using Moq;
using Xunit;
namespace LifeOS.Tests;
public class MissionWorkflowTests
{
    private const string AiJson = """{"priorities":[{"title":"Practice","category":"coding","priority":"high","reason":"Review C++","completed":true},{"title":"Reflect","category":"personal","priority":"low","reason":"Review the day"}],"summary":"Focused","warning":"Review first"}""";
    private sealed class Fixture : IDisposable
    {
        public readonly AppDbContext Db = new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        public readonly Guid User = Guid.NewGuid(); public LocalDay Day = HabitApiTests.Factory.Day;
        public readonly Mock<IAiProvider> Ai = new(); public readonly Mock<IGoogleCalendarService> Google = new(); public readonly AiCoachService Coach; public readonly PlanningService Planning;
        public Fixture() { var days = new Mock<ILocalDayService>(); days.Setup(d => d.TodayAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => Day); Ai.Setup(a => a.CompleteJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(AiJson); Google.Setup(g => g.StatusAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(new GoogleCalendarStatus(false,false,null,null,null,[],false,null,null)); Google.Setup(g => g.EventsAsync(It.IsAny<Guid>(),It.IsAny<DateTimeOffset>(),It.IsAny<DateTimeOffset>(),false,It.IsAny<CancellationToken>())).ReturnsAsync(new List<GoogleEventDto>()); Coach = new(Db, days.Object, Google.Object, () => Ai.Object, NullLogger<AiCoachService>.Instance); Planning = new(Db, days.Object, new CalendarService(Db), Google.Object); }
        public void Dispose() => Db.Dispose();
    }
    [Fact]
    public async Task GenerationIsOnlyAProposalAndReviewedApplyIsRetrySafe()
    {
        using var f = new Fixture(); var p = await f.Coach.GenerateMissionAsync(f.User, default);
        Assert.Empty(f.Db.DailyMissions); Assert.Empty((await f.Planning.TodayAsync(f.User, default)).Actions); Assert.Empty(await f.Coach.InsightsAsync(f.User, 10, default)); Assert.False(p.Priorities[0].Completed);
        var tasks = p.Priorities.AsEnumerable().Reverse().ToList(); tasks[0].Title = "My reviewed task"; tasks[0].Priority = "high";
        var saved = await f.Coach.ApplyProposalAsync(f.User, p.Id, new() { Priorities = tasks, AiSummary = "My summary" }, default);
        Assert.Equal("My reviewed task", saved!.Priorities[0].Title); Assert.Equal("My summary", saved.AiSummary); Assert.Single(f.Db.DailyMissions); Assert.Single(await f.Coach.InsightsAsync(f.User, 10, default)); Assert.Null(await f.Coach.ProposalAsync(f.User, default));
        var retry = await f.Coach.ApplyProposalAsync(f.User, p.Id, new() { Priorities = [] }, default); Assert.Equal(saved.Id, retry!.Id); Assert.Single(f.Db.DailyMissions);
        Assert.Null(await f.Coach.ApplyProposalAsync(Guid.NewGuid(), p.Id, new() { Priorities = tasks }, default));
    }
    [Fact]
    public async Task StableTaskIdsSurviveReorderingAndCompletionAndStaleEditsFail()
    {
        using var f = new Fixture(); var p = await f.Coach.GenerateMissionAsync(f.User, default); var m = (await f.Coach.ApplyProposalAsync(f.User,p.Id,new(){Priorities=p.Priorities},default))!; var target = m.Priorities[0].Id;
        var moved = (await f.Coach.EditMissionAsync(f.User,m.Id,new(){ExpectedUpdatedAt=m.UpdatedAt,Priorities=m.Priorities.AsEnumerable().Reverse().ToList()},default))!;
        await Assert.ThrowsAsync<RecordConflictException>(()=>f.Coach.EditMissionAsync(f.User,m.Id,new(){ExpectedUpdatedAt=m.UpdatedAt,Priorities=m.Priorities},default));
        Assert.True(await f.Planning.CompleteAsync(f.User,$"mission:{m.Id}:{target}",default)); var current=(await f.Coach.MissionAsync(f.User,default))!;
        Assert.True(current.Priorities.Single(i=>i.Id==target).Completed); Assert.False(current.Priorities[0].Completed);
        await Assert.ThrowsAsync<RecordConflictException>(()=>f.Planning.CompleteAsync(f.User,$"mission:{m.Id}:0",default));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Coach.EditMissionAsync(f.User,m.Id,new(){ExpectedUpdatedAt=current.UpdatedAt,Priorities=current.Priorities.Where(i=>!i.Completed).ToList()},default));
        Assert.Null(await f.Coach.EditMissionAsync(Guid.NewGuid(),m.Id,new(){Priorities=current.Priorities},default));
    }
    [Fact]
    public async Task LegacyTasksGetStableIdsAndNewTasksCannotForgeCompletion()
    {
        using var f = new Fixture(); var entity=new DailyMission{UserId=f.User,MissionDate=new DateTimeOffset(DateTime.SpecifyKind(f.Day.Date,DateTimeKind.Utc)),PrioritiesJson="""[{"title":"Original","category":"personal","priority":"medium","reason":"Legacy"}]"""}; f.Db.DailyMissions.Add(entity);await f.Db.SaveChangesAsync();
        var m=(await f.Coach.MissionAsync(f.User,default))!; Assert.Equal(m.Priorities[0].Id,(await f.Coach.MissionAsync(f.User,default))!.Priorities[0].Id);
        m.Priorities.Add(new(){Title="New",Category="personal",Priority="low",Reason="Added",Completed=true});var updated=(await f.Coach.EditMissionAsync(f.User,m.Id,new(){ExpectedUpdatedAt=m.UpdatedAt,Priorities=m.Priorities},default))!;
        Assert.All(updated.Priorities,i=>Assert.False(i.Completed));Assert.Equal(2,updated.Priorities.Select(i=>i.Id).Distinct().Count());
        Assert.True(await f.Planning.CompleteAsync(f.User,$"mission:{m.Id}:{m.Priorities[0].Id}",default));
        var complete=(await f.Coach.CompleteMissionAsync(f.User,m.Id,default))!;Assert.All(complete.Priorities,i=>Assert.True(i.Completed));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Coach.EditMissionAsync(f.User,m.Id,new(){ExpectedUpdatedAt=complete.UpdatedAt,Priorities=complete.Priorities},default));
    }
    [Fact]
    public async Task DiscardedAndExpiredProposalsCannotApplyAndInvalidReviewDoesNotSave()
    {
        using var f = new Fixture(); var p=await f.Coach.GenerateMissionAsync(f.User,default);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Coach.ApplyProposalAsync(f.User,p.Id,new(){Priorities=[]},default));Assert.Empty(f.Db.DailyMissions);
        f.Day=f.Day.Offset(1);await Assert.ThrowsAsync<RecordConflictException>(()=>f.Coach.ApplyProposalAsync(f.User,p.Id,new(){Priorities=p.Priorities},default));Assert.Null(await f.Coach.ProposalAsync(f.User,default));
        var next=await f.Coach.GenerateMissionAsync(f.User,default);Assert.NotEqual(p.Id,next.Id);Assert.False(await f.Coach.DiscardProposalAsync(Guid.NewGuid(),next.Id,default));Assert.True(await f.Coach.DiscardProposalAsync(f.User,next.Id,default));
        await Assert.ThrowsAsync<RecordConflictException>(()=>f.Coach.ApplyProposalAsync(f.User,next.Id,new(){Priorities=next.Priorities},default));Assert.Empty(f.Db.DailyMissions);
    }
    [Fact]
    public async Task ApiRequiresReviewBeforeCreatingTheDailyPlan()
    {
        using var app=new Factory();using var client=app.CreateClient();var response=await client.PostAsync("/api/aicoach/generate-mission",null);response.EnsureSuccessStatusCode();var p=(await response.Content.ReadFromJsonAsync<MissionProposalDto>())!;
        Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync("/api/aicoach/mission")).StatusCode);
        var applied=await client.PostAsJsonAsync($"/api/aicoach/mission/proposal/{p.Id}/apply",new{priorities=p.Priorities,aiSummary="Reviewed"});applied.EnsureSuccessStatusCode();var m=(await applied.Content.ReadFromJsonAsync<DailyMissionDto>())!;
        Assert.Equal(HttpStatusCode.Conflict,(await client.PutAsJsonAsync($"/api/aicoach/mission/{m.Id}",new{priorities=m.Priorities})).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await client.PostAsJsonAsync($"/api/aicoach/mission/proposal/{Guid.NewGuid()}/apply",new{priorities=p.Priorities})).StatusCode);
    }
    private sealed class Factory : HabitApiTests.Factory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder){base.ConfigureWebHost(builder);builder.ConfigureServices(services=>{services.RemoveAll<IAiProvider>();var ai=new Mock<IAiProvider>();ai.Setup(a=>a.CompleteJsonAsync(It.IsAny<string>(),It.IsAny<string>(),It.IsAny<CancellationToken>())).ReturnsAsync(AiJson);services.AddSingleton(ai.Object);});}
    }
}
