using System.Net;
using System.Net.Http.Json;
using LifeOS.Application.DTOs.Calendar;
using LifeOS.Application.DTOs.Coach;
using LifeOS.Application.Interfaces;
using LifeOS.Application.Services;
using LifeOS.Domain.Entities;
using LifeOS.Infrastructure.Coach;
using LifeOS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.Hosting;
using Moq;
using Xunit;
namespace LifeOS.Tests;
public class AiCoachTests
{
    private sealed class Fixture : IDisposable
    {
        public readonly Guid User=Guid.NewGuid();public readonly AppDbContext Db=new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        public readonly Mock<IAiProvider> Ai=new();public readonly Mock<IGoogleCalendarService> Google=new();public readonly AiCoachService Coach;
        public Fixture(){var days=new Mock<ILocalDayService>();days.Setup(x=>x.TodayAsync(It.IsAny<Guid>(),It.IsAny<CancellationToken>())).ReturnsAsync(HabitApiTests.Factory.Day);Google.Setup(g=>g.StatusAsync(It.IsAny<Guid>(),It.IsAny<CancellationToken>())).ReturnsAsync(new GoogleCalendarStatus(false,false,null,null,null,[],false,null,null));Coach=new(Db,days.Object,Google.Object,()=>Ai.Object,NullLogger<AiCoachService>.Instance);Ai.Setup(a=>a.CompleteAsync(It.IsAny<string>(),It.IsAny<string>(),It.IsAny<CancellationToken>())).ReturnsAsync("A grounded response");}
        public void Dispose()=>Db.Dispose();
    }
    [Fact]
    public async Task ContextIsOwnedAndJournalRequiresOptIn()
    {
        using var f=new Fixture();var foreign=Guid.NewGuid();f.Db.UserProfiles.Add(new(){UserId=f.User,TargetRoles="Embedded C++"});f.Db.Skills.Add(new(){UserId=foreign,Name="FOREIGN_PRIVATE_SKILL"});f.Db.JournalEntries.Add(new(){UserId=f.User,Title="Private",Content="OWN_JOURNAL_TEXT"});f.Db.JournalEntries.Add(new(){UserId=foreign,Content="FOREIGN_JOURNAL"});await f.Db.SaveChangesAsync();
        await f.Coach.SaveMemoryAsync(f.User,null,new("OWN_SAVED_NOTE"),default);await f.Coach.SaveMemoryAsync(foreign,null,new("FOREIGN_MEMORY"),default);
        var content=System.Text.Json.JsonSerializer.Serialize(await f.Coach.ContextAsync(f.User,default));Assert.Contains("OWN_SAVED_NOTE",content);Assert.Contains("Embedded",content);Assert.DoesNotContain("OWN_JOURNAL_TEXT",content);Assert.DoesNotContain("FOREIGN_",content);
        await f.Coach.SavePreferencesAsync(f.User,new(true,true,false,true),default);content=System.Text.Json.JsonSerializer.Serialize(await f.Coach.ContextAsync(f.User,default));Assert.Contains("OWN_JOURNAL_TEXT",content);Assert.DoesNotContain("FOREIGN_",content);
    }
    [Fact]
    public async Task GoogleTitlesAreHiddenUntilTheCalendarSharingOptIn()
    {
        using var f=new Fixture();var status=new GoogleCalendarStatus(true,true,"lifeos",null,null,[],false,null,null);f.Google.Setup(g=>g.StatusAsync(f.User,It.IsAny<CancellationToken>())).ReturnsAsync(status);
        f.Google.Setup(g=>g.EventsAsync(f.User,It.IsAny<DateTimeOffset>(),It.IsAny<DateTimeOffset>(),false,It.IsAny<CancellationToken>())).ReturnsAsync([new(){Id="event",Title="PRIVATE_GOOGLE_TITLE",Description="PRIVATE_DESCRIPTION",StartTime=HabitApiTests.Factory.Day.Start.AddHours(10)}]);
        var text=System.Text.Json.JsonSerializer.Serialize(await f.Coach.ContextAsync(f.User,default));Assert.Contains("Busy",text);Assert.DoesNotContain("PRIVATE_",text);
        f.Google.Setup(g=>g.StatusAsync(f.User,It.IsAny<CancellationToken>())).ReturnsAsync(status with{ShareDetailsWithCoach=true});text=System.Text.Json.JsonSerializer.Serialize(await f.Coach.ContextAsync(f.User,default));Assert.Contains("PRIVATE_GOOGLE_TITLE",text);Assert.DoesNotContain("PRIVATE_DESCRIPTION",text);
    }
    [Fact]
    public async Task ChatIncludesRecentTurnsAndReplaysWithoutAnotherAiCall()
    {
        using var f=new Fixture();var prompts=new List<string>();f.Ai.Setup(a=>a.CompleteAsync(It.IsAny<string>(),It.IsAny<string>(),It.IsAny<CancellationToken>())).Callback<string,string,CancellationToken>((_,p,_)=>prompts.Add(p)).ReturnsAsync("Response mentions the earlier question");
        f.Db.AiCoachMessages.Add(new(){UserId=Guid.NewGuid(),MessageType="coach_user",Content="FOREIGN_CHAT_SECRET"});await f.Db.SaveChangesAsync();
        var id=Guid.NewGuid();await f.Coach.ChatAsync(f.User,new("My focus is embedded systems",id),default);var replay=await f.Coach.ChatAsync(f.User,new("My focus is embedded systems",id),default);Assert.True(replay.WasReplay);Assert.Single(prompts);
        await f.Coach.ChatAsync(f.User,new("What should I practice next?",Guid.NewGuid()),default);Assert.Contains("My focus is embedded systems",prompts[1]);Assert.Contains("Response mentions the earlier question",prompts[1]);Assert.DoesNotContain("FOREIGN_CHAT_SECRET",prompts[1]);
        var history=await f.Coach.HistoryAsync(f.User,default);Assert.Equal(new[]{"user","assistant","user","assistant"},history.Select(h=>h.Role));Assert.Equal(4,history.Count);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Coach.ChatAsync(f.User,new("Changed request text",id),default));
    }
    [Fact]
    public async Task FailedResponsesAreNotStoredAndNotesAreExplicitlyManaged()
    {
        using var f=new Fixture();f.Ai.Setup(a=>a.CompleteAsync(It.IsAny<string>(),It.IsAny<string>(),It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException("synthetic provider error"));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Coach.ChatAsync(f.User,new("A message",Guid.NewGuid()),default));Assert.Empty(await f.Coach.HistoryAsync(f.User,default));
        var note=await f.Coach.SaveMemoryAsync(f.User,null,new("A deliberate saved goal"),default);Assert.Null(await f.Coach.SaveMemoryAsync(Guid.NewGuid(),note!.Id,new("Foreign edit"),default));Assert.False(await f.Coach.DeleteMemoryAsync(Guid.NewGuid(),note.Id,default));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Coach.SaveMemoryAsync(f.User,null,new(new string('x',501)),default));Assert.Empty(await f.Coach.InsightsAsync(f.User,10,default));
        await f.Coach.ClearHistoryAsync(f.User,default);Assert.Single(await f.Coach.MemoriesAsync(f.User,default));Assert.True(await f.Coach.DeleteMemoryAsync(f.User,note.Id,default));
    }
    [Fact]
    public async Task ConcurrentMissionRequestsGenerateOnceAndUseTheLocalDate()
    {
        using var f=new Fixture();f.Ai.Setup(a=>a.CompleteJsonAsync(It.IsAny<string>(),It.IsAny<string>(),It.IsAny<CancellationToken>())).ReturnsAsync("""{"priorities":[{"title":"Practice C++","category":"coding","priority":"high","reason":"Your saved goal","completed":true}],"summary":"A focused day"}""");
        var results=await Task.WhenAll(Enumerable.Range(0,5).Select(_=>f.Coach.GenerateMissionAsync(f.User,default)));Assert.Single(results.Select(m=>m.Id).Distinct());Assert.Equal(HabitApiTests.Factory.Day.Date,results[0].Date.Date);Assert.False(results[0].Priorities[0].Completed);
        f.Ai.Verify(a=>a.CompleteJsonAsync(It.IsAny<string>(),It.IsAny<string>(),It.IsAny<CancellationToken>()),Times.Once);
    }
    [Fact]
    public async Task MissionsUseRecentConversationAndRejectIncompleteAiOutput()
    {
        using var f=new Fixture();await f.Coach.ChatAsync(f.User,new("I have only 20 minutes available tonight",Guid.NewGuid()),default);
        string prompt="";f.Ai.Setup(a=>a.CompleteJsonAsync(It.IsAny<string>(),It.IsAny<string>(),It.IsAny<CancellationToken>())).Callback<string,string,CancellationToken>((_,p,_)=>prompt=p).ReturnsAsync("""{"priorities":[null]}""");
        await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Coach.GenerateMissionAsync(f.User,default));Assert.Contains("20 minutes",prompt);Assert.Empty(f.Db.DailyMissions);
    }
    [Fact]
    public async Task ApiHistoryMemoryAndPreferencesAreIsolatedAndClearKeepsNotes()
    {
        using var app=new CoachFactory();using var client=app.CreateClient();
        var saved=await client.PostAsJsonAsync("/api/aicoach/memories",new{content="Focus on C++"});Assert.Equal(HttpStatusCode.OK,saved.StatusCode);
        var requestId=Guid.NewGuid();Assert.Equal(HttpStatusCode.OK,(await client.PostAsJsonAsync("/api/aicoach/chat",new{message="Help me practice",requestId})).StatusCode);
        Assert.Contains("Help me practice",await client.GetStringAsync("/api/aicoach/history"));Assert.DoesNotContain("Focus on C++",await client.GetStringAsync("/api/aicoach/messages"));
        Assert.Equal(HttpStatusCode.NoContent,(await client.PutAsJsonAsync("/api/aicoach/preferences",new{includeProfile=false,includeProgress=false,includeCalendar=false,includeJournal=false})).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,(await client.DeleteAsync("/api/aicoach/history")).StatusCode);Assert.Equal("[]",await client.GetStringAsync("/api/aicoach/history"));Assert.Contains("Focus on C++",await client.GetStringAsync("/api/aicoach/memories"));
        Assert.Equal(HttpStatusCode.NotFound,(await client.DeleteAsync("/api/aicoach/memories/"+Guid.NewGuid())).StatusCode);Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync("/api/aicoach/chat",new{message=""})).StatusCode);
    }
    private class CoachFactory : HabitApiTests.Factory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder){base.ConfigureWebHost(builder);builder.ConfigureServices(services=>{services.RemoveAll<IAiProvider>();var ai=new Mock<IAiProvider>();ai.Setup(a=>a.CompleteAsync(It.IsAny<string>(),It.IsAny<string>(),It.IsAny<CancellationToken>())).ReturnsAsync("Synthetic coach reply");services.AddSingleton(ai.Object);});}
    }
}
