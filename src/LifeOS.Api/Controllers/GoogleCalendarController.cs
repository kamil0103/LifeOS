using System.Security.Cryptography;
using System.Text;
using LifeOS.Application.DTOs.Calendar;
using LifeOS.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
namespace LifeOS.Api.Controllers;
[ApiController,Route("api/googlecalendar"),Authorize]
public class GoogleCalendarController(IGoogleCalendarService google,ICurrentUserService user) : ControllerBase
{
    private const string CookieName="lifeos-google-calendar-state";
    private Guid UserId=>user.UserId??throw new UnauthorizedAccessException();
    [HttpGet("status")]
    public Task<GoogleCalendarStatus> Status(CancellationToken ct)=>google.StatusAsync(UserId,ct);
    [HttpPost("connect")]
    public IActionResult Connect()
    {
        Response.Headers.CacheControl="no-store";
        var url=google.ConnectUrl(UserId);
        var state=QueryHelpers.ParseQuery(new Uri(url).Query)["state"].ToString();
        Response.Cookies.Append(CookieName,Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(state))),new CookieOptions {HttpOnly=true,Secure=google.ReturnUrl.StartsWith("https://",StringComparison.Ordinal),SameSite=SameSiteMode.Lax,Path="/api/googlecalendar",MaxAge=TimeSpan.FromMinutes(10),IsEssential=true});
        return Ok(new{url});
    }
    [HttpGet("callback"),AllowAnonymous]
    public async Task<IActionResult> Callback([FromQuery]string? state,[FromQuery]string? code,[FromQuery]string? error,CancellationToken ct)
    {
        Response.Headers.CacheControl="no-store";
        var expected=string.IsNullOrEmpty(state)?"":Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(state)));
        var cookie=Request.Cookies[CookieName]??"";
        if(cookie.Length!=expected.Length || cookie.Length==0 || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(cookie),Encoding.UTF8.GetBytes(expected)))return BadRequest("Google sign-in expired. Start the connection again from LifeOS.");
        Response.Cookies.Delete(CookieName,new CookieOptions{Path="/api/googlecalendar"});
        if(!string.IsNullOrEmpty(error))return Redirect(google.ReturnUrl+"?google=cancelled");
        if(string.IsNullOrEmpty(code))return BadRequest("Google authorization code is missing.");
        try{await google.CompleteConnectionAsync(state!,code,ct);return Redirect(google.ReturnUrl+"?google=connected");}
        catch(InvalidOperationException){return Redirect(google.ReturnUrl+"?google=failed");}
    }
    [HttpPost("disconnect")]
    public async Task<IActionResult> Disconnect(CancellationToken ct){await google.DisconnectAsync(UserId,ct);return NoContent();}
    [HttpPut("settings")]
    public async Task<IActionResult> Settings(GoogleCalendarSettings settings,CancellationToken ct){await google.SettingsAsync(UserId,settings,ct);return NoContent();}
    [HttpGet("events")]
    public Task<List<GoogleEventDto>> Events([FromQuery]DateTimeOffset start,[FromQuery]DateTimeOffset end,[FromQuery]bool refresh,CancellationToken ct)=>google.EventsAsync(UserId,start,end,refresh,ct);
    [HttpPost("events")]
    public Task<GoogleEventDto> Create(GoogleEventRequest request,CancellationToken ct)=>google.CreateAsync(UserId,request,ct);
    [HttpPut("events/{id}")]
    public Task<GoogleEventDto> Update(string id,GoogleEventRequest request,CancellationToken ct)=>google.UpdateAsync(UserId,id,request,ct);
    [HttpDelete("events/{id}")]
    public async Task<IActionResult> Delete(string id,[FromQuery]string etag,CancellationToken ct){await google.DeleteAsync(UserId,id,etag,ct);return NoContent();}
}
