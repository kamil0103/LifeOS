using System.Net;
using System.Net.Http.Json;
using LifeOS.Domain.Entities;
using LifeOS.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace LifeOS.Tests;
public class CalendarApiTests
{
    [Fact]
    public async Task EventLifecycleAndValidation()
    {
        using var app=new HabitApiTests.Factory();using var client=app.CreateClient();
        var start=DateTimeOffset.Parse("2026-10-15T16:00:00Z");
        var created=await client.PostAsJsonAsync("/api/calendar/events",new{title="Test interview",startTime=start,endTime=start.AddHours(1),eventType="job"});
        Assert.Equal(HttpStatusCode.OK,created.StatusCode);
        var doc=await created.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();var id=doc.GetProperty("id").GetGuid();
        var updated=await client.PutAsJsonAsync($"/api/calendar/events/{id}",new{title="Updated interview",startTime=start,endTime=start.AddHours(2),eventType="job"});
        Assert.Equal(HttpStatusCode.OK,updated.StatusCode);
        var invalid=await client.PutAsJsonAsync($"/api/calendar/events/{id}",new{title="Invalid",startTime=start,endTime=start.AddHours(-1)});
        Assert.Equal(HttpStatusCode.BadRequest,invalid.StatusCode);
        var overlap=await client.GetAsync("/api/calendar/events?start=2026-10-15T17:00:00Z&end=2026-10-15T19:00:00Z");
        Assert.Contains("Updated interview",await overlap.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NoContent,(await client.DeleteAsync($"/api/calendar/events/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await client.DeleteAsync($"/api/calendar/events/{id}")).StatusCode);
    }
    [Fact]
    public async Task ForeignEventsAndLinkedRecordsAreProtected()
    {
        using var app=new HabitApiTests.Factory();using var client=app.CreateClient();Guid id,habitId;
        using(var scope=app.Services.CreateScope()){
            var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();var habit=new Habit{UserId=Guid.NewGuid(),Name="Private"};db.Habits.Add(habit);habitId=habit.Id;
            var item=new CalendarEvent{UserId=habit.UserId,Title="Private event",StartTime=DateTimeOffset.UtcNow};db.CalendarEvents.Add(item);id=item.Id;await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.NotFound,(await client.DeleteAsync($"/api/calendar/events/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync("/api/calendar/events",new{title="Invalid link",startTime=DateTimeOffset.UtcNow,habitId})).StatusCode);
    }
    [Fact]
    public async Task CallbackWithoutBrowserBindingIsRejected()
    {
        using var app=new HabitApiTests.Factory();using var client=app.CreateClient();
        Assert.Equal(HttpStatusCode.BadRequest,(await client.GetAsync("/api/googlecalendar/callback?state=forged&code=forged")).StatusCode);
    }
}
