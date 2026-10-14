using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LifeOS.Application.DTOs.Calendar;
using LifeOS.Application.Interfaces;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
namespace LifeOS.Infrastructure.Calendar;

public class GoogleCalendarService : IGoogleCalendarService
{
    private const string Scopes = "openid email https://www.googleapis.com/auth/calendar.readonly https://www.googleapis.com/auth/calendar.app.created";
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> Gates = new();
    private static readonly ConcurrentDictionary<string, Ticket> Tickets = new();
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly IHttpClientFactory _http;
    private readonly IConfiguration _config;
    private readonly IDataProtector _protector;
    private readonly IDataProtector _state;
    private readonly ILogger<GoogleCalendarService> _logger;
    private string Root => _config["GoogleCalendar:StoragePath"] ?? "/app/data/integrations/google-calendar";
    private string ClientId => _config["GoogleCalendar:ClientId"] ?? "";
    private string ClientSecret => _config["GoogleCalendar:ClientSecret"] ?? "";
    private string RedirectUri => _config["GoogleCalendar:RedirectUri"] ?? "";
    private bool Configured => !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret) && Uri.TryCreate(RedirectUri,UriKind.Absolute,out var uri) && (uri.Scheme=="https" || uri.Scheme=="http" && uri.IsLoopback);
    public string ReturnUrl => Configured ? new Uri(RedirectUri).GetLeftPart(UriPartial.Authority)+"/calendar" : "/calendar";
    public GoogleCalendarService(IHttpClientFactory http,IConfiguration config,IDataProtectionProvider protection,ILogger<GoogleCalendarService> logger)
    { _http=http;_config=config;_protector=protection.CreateProtector("LifeOS.GoogleCalendar.Account.v1");_state=protection.CreateProtector("LifeOS.GoogleCalendar.OAuth.v1");_logger=logger; }
    private string FilePath(Guid userId) => Path.Combine(Root,userId.ToString("N")+".protected");
    private async Task<Account?> Load(Guid id,CancellationToken ct)
    {
        var path=FilePath(id); if(!File.Exists(path))return null;
        try {return JsonSerializer.Deserialize<Account>(_protector.Unprotect(await File.ReadAllTextAsync(path,ct)),Json);}
        catch(CryptographicException){throw new InvalidOperationException("Google connection keys could not be read. Restore the integration keys or reconnect.");}
    }
    private async Task Save(Guid id,Account account,CancellationToken ct)
    {
        Directory.CreateDirectory(Root);
        if(!OperatingSystem.IsWindows())File.SetUnixFileMode(Root,UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.UserExecute);
        var temp=FilePath(id)+".tmp";
        await File.WriteAllTextAsync(temp,_protector.Protect(JsonSerializer.Serialize(account,Json)),ct);
        if(!OperatingSystem.IsWindows())File.SetUnixFileMode(temp,UnixFileMode.UserRead|UnixFileMode.UserWrite);
        File.Move(temp,FilePath(id),true);
    }
    private async Task<T> Locked<T>(Guid id,Func<Task<T>> action,CancellationToken ct)
    {
        var gate=Gates.GetOrAdd(id,_=>new(1,1));await gate.WaitAsync(ct);
        try{return await action();}finally{gate.Release();}
    }
    public Task<GoogleCalendarStatus> StatusAsync(Guid userId,CancellationToken ct)=>Locked(userId,async()=>
    {
        var account=await Load(userId,ct);
        return new GoogleCalendarStatus(Configured,account!=null,account?.CalendarId,account?.LastSyncAt,account?.Error,
            account?.Calendars ?? [],account?.ShareDetailsWithCoach ?? false,Configured?RedirectUri:null,account?.Email);
    },ct);
    public string ConnectUrl(Guid userId)
    {
        if(!Configured)throw new InvalidOperationException("Google Calendar setup is incomplete. Configure its OAuth client and callback URL first.");
        foreach(var old in Tickets.Where(t=>t.Value.Expires<DateTimeOffset.UtcNow).Select(t=>t.Key))Tickets.TryRemove(old,out _);
        var nonce=WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var verifier=WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(48));
        Tickets[nonce]=new(userId,verifier,DateTimeOffset.UtcNow.AddMinutes(10));
        return QueryHelpers.AddQueryString("https://accounts.google.com/o/oauth2/v2/auth",new Dictionary<string,string?> {
            ["client_id"]=ClientId,["redirect_uri"]=RedirectUri,["response_type"]="code",["scope"]=Scopes,
            ["access_type"]="offline",["prompt"]="consent select_account",["state"]=_state.Protect(nonce),
            ["code_challenge"]=WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier))),["code_challenge_method"]="S256"
        });
    }
    public async Task CompleteConnectionAsync(string state,string code,CancellationToken ct)
    {
        if(!Configured)throw new InvalidOperationException("Google Calendar is not configured.");
        string nonce;try{nonce=_state.Unprotect(state);}catch(CryptographicException){throw new InvalidOperationException("Google sign-in expired. Start the connection again.");}
        if(!Tickets.TryRemove(nonce,out var ticket) || ticket.Expires<DateTimeOffset.UtcNow)throw new InvalidOperationException("Google sign-in expired. Start the connection again.");
        await Locked(ticket.UserId,async()=>
        {
            var token=await Token(new Dictionary<string,string> { ["grant_type"]="authorization_code",["code"]=code,["redirect_uri"]=RedirectUri,["code_verifier"]=ticket.Verifier },ct);
            if(string.IsNullOrWhiteSpace(token.RefreshToken))throw new InvalidOperationException("Google did not grant background access. Reconnect and approve calendar access.");
            if(!token.Scope.Contains("calendar.readonly") || !token.Scope.Contains("calendar.app.created"))throw new InvalidOperationException("Approve both calendar permissions to connect LifeOS.");
            var account=new Account { AccessToken=token.AccessToken,RefreshToken=token.RefreshToken,Expires=DateTimeOffset.UtcNow.AddSeconds(token.ExpiresIn) };
            var identity=await Send(account,HttpMethod.Get,"https://openidconnect.googleapis.com/v1/userinfo",null,null,ct);
            account.Subject=Text(identity,"sub");account.Email=Text(identity,"email");
            if(string.IsNullOrEmpty(account.Subject))throw new InvalidOperationException("Could not verify the Google account.");
            var old=await Load(ticket.UserId,ct);
            if(old?.Subject==account.Subject){account.CalendarId=old.CalendarId;account.SelectedIds=old.SelectedIds;account.ShareDetailsWithCoach=old.ShareDetailsWithCoach;}
            if(string.IsNullOrEmpty(account.CalendarId)){
                var calendar=await Send(account,HttpMethod.Post,"https://www.googleapis.com/calendar/v3/calendars",new{summary="LifeOS",description="Interviews, follow-ups, and study sessions managed in LifeOS."},null,ct);
                account.CalendarId=Text(calendar,"id");account.SelectedIds=[account.CalendarId];
            }
            await ReadCalendars(account,ct);await Save(ticket.UserId,account,ct);return true;
        },ct);
    }
    public Task DisconnectAsync(Guid userId,CancellationToken ct)=>Locked(userId,async()=>
    {
        var account=await Load(userId,ct);
        if(account!=null){
            using var response=await _http.CreateClient("GoogleCalendar").PostAsync("https://oauth2.googleapis.com/revoke",new FormUrlEncodedContent(new Dictionary<string,string>{["token"]=account.RefreshToken}),ct);
            if(!response.IsSuccessStatusCode && response.StatusCode!=HttpStatusCode.BadRequest)throw new InvalidOperationException("Google could not revoke the connection. Try disconnecting again.");
            File.Delete(FilePath(userId));
        }
        return true;
    },ct);
    public Task SettingsAsync(Guid userId,GoogleCalendarSettings settings,CancellationToken ct)=>Locked(userId,async()=>
    {
        var account=await Required(userId,ct);await ReadCalendars(account,ct);
        var allowed=account.Calendars.Select(c=>c.Id).ToHashSet();
        if(settings.CalendarIds.Count>20 || settings.CalendarIds.Any(id=>!allowed.Contains(id)))throw new InvalidOperationException("Choose calendars from your connected account.");
        account.SelectedIds=settings.CalendarIds.Distinct().ToList();account.ShareDetailsWithCoach=settings.ShareDetailsWithCoach;account.Events=[];account.WindowStart=null;account.WindowEnd=null;
        account.Calendars=account.Calendars.Select(c=>c with{Selected=account.SelectedIds.Contains(c.Id)}).ToList();
        await Save(userId,account,ct);return true;
    },ct);
    public Task<List<GoogleEventDto>> EventsAsync(Guid userId,DateTimeOffset start,DateTimeOffset end,bool refresh,CancellationToken ct)=>Locked(userId,async()=>
    {
        if(end<=start || end-start>TimeSpan.FromDays(370))throw new InvalidOperationException("Choose a calendar range of at most one year.");
        var account=await Load(userId,ct);if(account==null || !Configured)return new List<GoogleEventDto>();
        if(!refresh && account.WindowStart<=start && account.WindowEnd>=end && account.LastSyncAt>DateTimeOffset.UtcNow.AddMinutes(-5))return account.Events.Where(e=>e.StartTime<end && (e.EndTime??e.StartTime)>=start).ToList();
        try{
            await FreshAccess(account,ct);await ReadCalendars(account,ct);
            var events=new List<GoogleEventDto>();
            foreach(var calendar in account.Calendars.Where(c=>c.Selected)){
                string? page=null;var pageCount=0;
                do{
                    if(++pageCount>100)throw new InvalidOperationException("Too many events for this range. Choose a shorter period.");
                    var url=QueryHelpers.AddQueryString(CalendarUrl(calendar.Id)+"/events",new Dictionary<string,string?>{["timeMin"]=start.ToUniversalTime().ToString("O"),["timeMax"]=end.ToUniversalTime().ToString("O"),["singleEvents"]="true",["orderBy"]="startTime",["maxResults"]="2500",["pageToken"]=page});
                    var body=await Send(account,HttpMethod.Get,url,null,null,ct);
                    if(body.TryGetProperty("items",out var items))foreach(var item in items.EnumerateArray())if(Text(item,"status")!="cancelled")events.Add(Map(item,calendar));
                    page=Text(body,"nextPageToken");
                }while(!string.IsNullOrEmpty(page));
            }
            account.Events=events;account.WindowStart=start;account.WindowEnd=end;account.LastSyncAt=DateTimeOffset.UtcNow;account.Error=null;await Save(userId,account,ct);return events;
        }catch(InvalidOperationException ex){account.Error=ex.Message;await Save(userId,account,ct);throw;}
        catch(Exception ex) when ((ex is HttpRequestException || ex is OperationCanceledException) && !ct.IsCancellationRequested){account.Error="Google Calendar could not be reached. Try refreshing again.";await Save(userId,account,ct);throw new InvalidOperationException(account.Error);}
    },ct);
    public Task<GoogleEventDto> CreateAsync(Guid userId,GoogleEventRequest request,CancellationToken ct)=>Write(userId,null,request,ct);
    public Task<GoogleEventDto> UpdateAsync(Guid userId,string eventId,GoogleEventRequest request,CancellationToken ct)=>Write(userId,eventId,request,ct);
    private Task<GoogleEventDto> Write(Guid userId,string? eventId,GoogleEventRequest request,CancellationToken ct)=>Locked(userId,async()=>
    {
        Validate(request);
        var account=await Required(userId,ct);await FreshAccess(account,ct);
        if(eventId!=null && string.IsNullOrWhiteSpace(request.Etag))throw new InvalidOperationException("Reload the event before editing it.");
        object start=request.IsAllDay?new{date=request.StartDate}:(object)new{dateTime=request.StartTime.ToUniversalTime().ToString("O")};
        object end=request.IsAllDay?new{date=request.EndDate}:(object)new{dateTime=(request.EndTime??request.StartTime.AddHours(1)).ToUniversalTime().ToString("O")};
        var payload=new Dictionary<string,object?> { ["summary"]=request.Title.Trim(), ["description"]=request.Description, ["location"]=request.Location, ["start"]=start, ["end"]=end };
        if(eventId==null && !string.IsNullOrWhiteSpace(request.ActionKey))payload["id"]="lifeos"+Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(userId+":"+request.ActionKey))).ToLowerInvariant();
        var url=CalendarUrl(account.CalendarId)+"/events"+(eventId!=null?"/"+Uri.EscapeDataString(eventId):"");
        var body=await Send(account,eventId==null?HttpMethod.Post:HttpMethod.Patch,url,payload,eventId!=null?request.Etag:null,ct);
        if(!account.SelectedIds.Contains(account.CalendarId))account.SelectedIds.Add(account.CalendarId);
        account.Calendars=account.Calendars.Select(c=>c with{Selected=account.SelectedIds.Contains(c.Id)}).ToList();
        account.WindowStart=null;account.WindowEnd=null;await Save(userId,account,ct);
        return Map(body,new(account.CalendarId,"LifeOS",true,true,null));
    },ct);
    public Task DeleteAsync(Guid userId,string eventId,string etag,CancellationToken ct)=>Locked(userId,async()=>
    {
        if(string.IsNullOrWhiteSpace(etag))throw new InvalidOperationException("Reload the event before deleting it.");
        var account=await Required(userId,ct);await FreshAccess(account,ct);
        await Send(account,HttpMethod.Delete,CalendarUrl(account.CalendarId)+"/events/"+Uri.EscapeDataString(eventId),null,etag,ct);
        account.WindowStart=null;account.WindowEnd=null;await Save(userId,account,ct);return true;
    },ct);
    public async Task SyncAllAsync(CancellationToken ct)
    {
        if(!Configured || !Directory.Exists(Root))return;
        foreach(var path in Directory.EnumerateFiles(Root,"*.protected"))if(Guid.TryParseExact(Path.GetFileNameWithoutExtension(path),"N",out var id)){
            try{await EventsAsync(id,DateTimeOffset.UtcNow.AddDays(-30),DateTimeOffset.UtcNow.AddDays(180),true,ct);}
            catch(OperationCanceledException)when(ct.IsCancellationRequested){throw;}
            catch(Exception){_logger.LogWarning("Google Calendar sync failed for a connected account; see its connection status.");}
        }
    }
    private async Task<Account> Required(Guid id,CancellationToken ct)=>await Load(id,ct)??throw new InvalidOperationException("Connect Google Calendar first.");
    private async Task ReadCalendars(Account account,CancellationToken ct)
    {
        await FreshAccess(account,ct);var list=new List<GoogleCalendarInfo>();string? page=null;var count=0;
        do{
            if(++count>20)throw new InvalidOperationException("Too many calendars in this account.");
            var body=await Send(account,HttpMethod.Get,QueryHelpers.AddQueryString("https://www.googleapis.com/calendar/v3/users/me/calendarList",new Dictionary<string,string?>{["maxResults"]="250",["pageToken"]=page}),null,null,ct);
            if(body.TryGetProperty("items",out var items))foreach(var item in items.EnumerateArray()){
                var id=Text(item,"id");list.Add(new(id,Text(item,"summary"),account.SelectedIds.Contains(id),id==account.CalendarId,Text(item,"timeZone")));
            }
            page=Text(body,"nextPageToken");
        }while(!string.IsNullOrEmpty(page));
        if(!list.Any(c=>c.Id==account.CalendarId))list.Add(new(account.CalendarId,"LifeOS",account.SelectedIds.Contains(account.CalendarId),true,null));
        account.Calendars=list;
    }
    private async Task FreshAccess(Account account,CancellationToken ct)
    {
        if(account.Expires>DateTimeOffset.UtcNow.AddMinutes(1))return;
        var token=await Token(new Dictionary<string,string>{["grant_type"]="refresh_token",["refresh_token"]=account.RefreshToken},ct);
        account.AccessToken=token.AccessToken;account.Expires=DateTimeOffset.UtcNow.AddSeconds(token.ExpiresIn);
        if(!string.IsNullOrEmpty(token.RefreshToken))account.RefreshToken=token.RefreshToken;
    }
    private async Task<TokenData> Token(Dictionary<string,string> parameters,CancellationToken ct)
    {
        parameters["client_id"]=ClientId;parameters["client_secret"]=ClientSecret;
        using var response=await _http.CreateClient("GoogleCalendar").PostAsync("https://oauth2.googleapis.com/token",new FormUrlEncodedContent(parameters),ct);
        if(!response.IsSuccessStatusCode)throw new InvalidOperationException("Google authorization failed. Reconnect Google Calendar.");
        using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));var root=json.RootElement;
        var access=Text(root,"access_token");if(string.IsNullOrEmpty(access))throw new InvalidOperationException("Google did not provide an access token.");
        return new(access,Text(root,"refresh_token"),root.TryGetProperty("expires_in",out var expiry)?expiry.GetInt32():3600,Text(root,"scope"));
    }
    private async Task<JsonElement> Send(Account account,HttpMethod method,string url,object? data,string? etag,CancellationToken ct)
    {
        using var request=new HttpRequestMessage(method,url);request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",account.AccessToken);
        if(etag!=null)request.Headers.TryAddWithoutValidation("If-Match",etag);
        if(data!=null)request.Content=new StringContent(JsonSerializer.Serialize(data,Json),Encoding.UTF8,"application/json");
        using var response=await _http.CreateClient("GoogleCalendar").SendAsync(request,ct);
        if(response.StatusCode==HttpStatusCode.Conflict)throw new CalendarEventAlreadyExistsException();
        if(response.StatusCode==HttpStatusCode.PreconditionFailed)throw new CalendarConflictException();
        if(response.StatusCode==HttpStatusCode.Unauthorized)throw new InvalidOperationException("Google access expired. Reconnect Google Calendar.");
        if(!response.IsSuccessStatusCode)throw new InvalidOperationException($"Google Calendar could not complete this action ({(int)response.StatusCode}). Check access and try again.");
        var body=await response.Content.ReadAsStringAsync(ct);if(string.IsNullOrWhiteSpace(body))return default;
        using var doc=JsonDocument.Parse(body);return doc.RootElement.Clone();
    }
    public static void Validate(GoogleEventRequest request)
    {
        if(request.IsAllDay){
            if(string.IsNullOrWhiteSpace(request.Title) || request.Title.Length>200 || !DateOnly.TryParseExact(request.StartDate,"yyyy-MM-dd",out var start) || !DateOnly.TryParseExact(request.EndDate,"yyyy-MM-dd",out var end) || end<=start)
                throw new InvalidOperationException("Enter a title and valid all-day dates. The end date is exclusive.");
        }else CalendarService.Validate(request.Title,request.StartTime,request.EndTime);
    }
    public static GoogleEventDto Map(JsonElement item,GoogleCalendarInfo calendar)
    {
        var start=item.GetProperty("start");var end=item.GetProperty("end");var allDay=start.TryGetProperty("date",out var sd);
        var zone=LifeOS.Application.Services.LocalDay.At(DateTimeOffset.UtcNow,calendar.TimeZone).Zone;
        DateTimeOffset ParseDate(string value)=>new(TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(DateTime.ParseExact(value,"yyyy-MM-dd",null),DateTimeKind.Unspecified),zone));
        var id=Text(item,"id");
        return new GoogleEventDto{Id="google:"+WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(calendar.Id+":"+id)),GoogleEventId=id,CalendarId=calendar.Id,CalendarName=calendar.Name,Title=Text(item,"summary") is {Length:>0} title?title:"Busy",Description=Text(item,"description"),Location=Text(item,"location"),IsAllDay=allDay,StartDate=allDay?sd.GetString():null,EndDate=allDay?Text(end,"date"):null,StartTime=allDay?ParseDate(sd.GetString()!):DateTimeOffset.Parse(Text(start,"dateTime")),EndTime=allDay?ParseDate(Text(end,"date")):DateTimeOffset.Parse(Text(end,"dateTime")),Editable=calendar.Editable,Etag=Text(item,"etag"),HtmlLink=Text(item,"htmlLink")};
    }
    private static string Text(JsonElement item,string name)=>item.ValueKind==JsonValueKind.Object && item.TryGetProperty(name,out var value) && value.ValueKind==JsonValueKind.String?value.GetString()??"":"";
    private static string CalendarUrl(string id)=>"https://www.googleapis.com/calendar/v3/calendars/"+Uri.EscapeDataString(id);
    private record Ticket(Guid UserId,string Verifier,DateTimeOffset Expires);
    private record TokenData(string AccessToken,string RefreshToken,int ExpiresIn,string Scope);
    public class Account
    {
        public string Subject{get;set;}="";public string Email{get;set;}="";public string AccessToken{get;set;}="";public string RefreshToken{get;set;}="";
        public DateTimeOffset Expires{get;set;}public string CalendarId{get;set;}="";public List<string> SelectedIds{get;set;}=[];
        public List<GoogleCalendarInfo> Calendars{get;set;}=[];public List<GoogleEventDto> Events{get;set;}=[];
        public DateTimeOffset? WindowStart{get;set;}public DateTimeOffset? WindowEnd{get;set;}public DateTimeOffset? LastSyncAt{get;set;}public string? Error{get;set;}public bool ShareDetailsWithCoach{get;set;}
    }
}
