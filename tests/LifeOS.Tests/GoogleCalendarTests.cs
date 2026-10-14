using System.Net;
using System.Text;
using System.Text.Json;
using LifeOS.Application.DTOs.Calendar;
using LifeOS.Application.Interfaces;
using LifeOS.Infrastructure.Calendar;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
namespace LifeOS.Tests;
public class GoogleCalendarTests
{
    private sealed class Fixture : IDisposable
    {
        public readonly string Root=Path.Combine(Path.GetTempPath(),"lifeos-calendar-test-"+Guid.NewGuid());
        public readonly Guid UserId=Guid.NewGuid();
        public readonly FakeGoogle Handler=new();
        public readonly GoogleCalendarService Service;
        public Fixture(){
            Directory.CreateDirectory(Root);
            var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["GoogleCalendar:ClientId"]="test-client",["GoogleCalendar:ClientSecret"]="test-secret",["GoogleCalendar:RedirectUri"]="https://lifeos.example.invalid/api/googlecalendar/callback",["GoogleCalendar:StoragePath"]=Root}).Build();
            var http=new Mock<IHttpClientFactory>();http.Setup(f=>f.CreateClient(It.IsAny<string>())).Returns(()=>new HttpClient(Handler,false));
            Service=new GoogleCalendarService(http.Object,config,DataProtectionProvider.Create(new DirectoryInfo(Path.Combine(Root,"keys"))),NullLogger<GoogleCalendarService>.Instance);
        }
        public async Task Connect(){var url=Service.ConnectUrl(UserId);var state=QueryHelpers.ParseQuery(new Uri(url).Query)["state"].ToString();await Service.CompleteConnectionAsync(state,"synthetic-code",default);}
        public void Dispose(){Directory.Delete(Root,true);}
    }
    private sealed class FakeGoogle : HttpMessageHandler
    {
        public bool Conflict;public bool Paginate;public int EventReads;public string? LastWriteUrl;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            var url=request.RequestUri!.ToString();string body;
            if(url.Contains("/token"))body="""{"access_token":"synthetic-access","refresh_token":"synthetic-refresh-secret","expires_in":3600,"scope":"https://www.googleapis.com/auth/calendar.readonly https://www.googleapis.com/auth/calendar.app.created"}""";
            else if(url.Contains("userinfo"))body="""{"sub":"synthetic-user","email":"test@example.invalid"}""";
            else if(url.Contains("calendarList"))body="""{"items":[{"id":"primary","summary":"Personal","timeZone":"America/Los_Angeles"},{"id":"lifeos-calendar","summary":"LifeOS","timeZone":"America/Los_Angeles"}]}""";
            else if(url.EndsWith("/calendars") && request.Method==HttpMethod.Post)body="""{"id":"lifeos-calendar"}""";
            else if(url.Contains("/events") && request.Method==HttpMethod.Get){
                EventReads++;
                if(Paginate && !url.Contains("pageToken="))body="""{"items":[],"nextPageToken":"second"}""";
                else body="""{"items":[{"id":"recurring_occurrence","summary":"Interview","etag":"\"v1\"","start":{"dateTime":"2026-10-15T09:00:00-07:00"},"end":{"dateTime":"2026-10-15T10:00:00-07:00"}}]}""";
            }else if(url.Contains("/events")){
                LastWriteUrl=url;
                if(Conflict)return Task.FromResult(new HttpResponseMessage(HttpStatusCode.PreconditionFailed));
                body="""{"id":"created","summary":"New event","etag":"\"v2\"","start":{"dateTime":"2026-10-15T09:00:00-07:00"},"end":{"dateTime":"2026-10-15T10:00:00-07:00"}}""";
            }else body="{}";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(body,Encoding.UTF8,"application/json")});
        }
    }
    [Fact]
    public async Task OAuthUsesPkceOneTimeStateAndEncryptedPersistence()
    {
        using var f=new Fixture();var url=f.Service.ConnectUrl(f.UserId);var query=QueryHelpers.ParseQuery(new Uri(url).Query);
        Assert.Equal("S256",query["code_challenge_method"].ToString());Assert.Contains("calendar.app.created",query["scope"].ToString());Assert.DoesNotContain("test-secret",url);
        var state=query["state"].ToString();await f.Service.CompleteConnectionAsync(state,"code",default);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Service.CompleteConnectionAsync(state,"replay",default));
        var status=await f.Service.StatusAsync(f.UserId,default);Assert.True(status.Connected);Assert.Equal("test@example.invalid",status.AccountEmail);Assert.False(status.ShareDetailsWithCoach);
        var stored=File.ReadAllText(Directory.GetFiles(f.Root,"*.protected").Single());Assert.DoesNotContain("synthetic-refresh-secret",stored);Assert.DoesNotContain("test@example.invalid",stored);
        Assert.False((await f.Service.StatusAsync(Guid.NewGuid(),default)).Connected);
    }
    [Fact]
    public async Task WritesAreLimitedToDedicatedCalendarAndConflictsAreRejected()
    {
        using var f=new Fixture();await f.Connect();
        var request=new GoogleEventRequest{Title="Interview",StartTime=DateTimeOffset.Parse("2026-10-15T16:00:00Z"),EndTime=DateTimeOffset.Parse("2026-10-15T17:00:00Z")};
        await f.Service.CreateAsync(f.UserId,request,default);Assert.Contains("/calendars/lifeos-calendar/events",f.Handler.LastWriteUrl);
        f.Handler.Conflict=true;request.Etag="\"v1\"";
        await Assert.ThrowsAsync<CalendarConflictException>(()=>f.Service.UpdateAsync(f.UserId,"event",request,default));
        Assert.Contains("/calendars/lifeos-calendar/events/event",f.Handler.LastWriteUrl);
    }
    [Fact]
    public async Task PaginationAndCacheAvoidDuplicateReads()
    {
        using var f=new Fixture();await f.Connect();f.Handler.Paginate=true;
        var start=DateTimeOffset.Parse("2026-10-01T00:00:00Z");var end=start.AddDays(31);
        var events=await f.Service.EventsAsync(f.UserId,start,end,true,default);
        Assert.Single(events);Assert.Equal("recurring_occurrence",events[0].GoogleEventId);Assert.True(events[0].Editable);Assert.Equal(2,f.Handler.EventReads);
        await f.Service.EventsAsync(f.UserId,start,end,false,default);Assert.Equal(2,f.Handler.EventReads);
    }
    [Fact]
    public void AllDayMappingUsesCalendarZoneAndExclusiveEndDate()
    {
        using var doc=JsonDocument.Parse("""{"id":"all-day","summary":"Day","start":{"date":"2026-03-08"},"end":{"date":"2026-03-09"}}""");
        var result=GoogleCalendarService.Map(doc.RootElement,new("primary","Personal",true,false,"America/Los_Angeles"));
        Assert.True(result.IsAllDay);Assert.False(result.Editable);Assert.Equal(23,(result.EndTime!.Value-result.StartTime).TotalHours);Assert.Equal("2026-03-09",result.EndDate);
    }
    [Fact]
    public void InvalidAllDayRangeIsRejected()
    {
        Assert.Throws<InvalidOperationException>(()=>GoogleCalendarService.Validate(new(){Title="Bad",IsAllDay=true,StartDate="2026-03-08",EndDate="2026-03-08"}));
    }
}
