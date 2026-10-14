namespace LifeOS.Application.DTOs.Calendar;
public record GoogleCalendarInfo(string Id, string Name, bool Selected, bool Editable, string? TimeZone);
public record GoogleCalendarStatus(bool Configured, bool Connected, string? CalendarId, DateTimeOffset? LastSyncAt, string? Error, List<GoogleCalendarInfo> Calendars, bool ShareDetailsWithCoach, string? RedirectUri, string? AccountEmail);
public class GoogleEventDto
{
    public string Id { get; set; } = "";
    public string GoogleEventId { get; set; } = "";
    public string CalendarId { get; set; } = "";
    public string CalendarName { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset? EndTime { get; set; }
    public bool IsAllDay { get; set; }
    public string? StartDate { get; set; }
    public string? EndDate { get; set; }
    public string? Location { get; set; }
    public string EventType { get; set; } = "general";
    public bool Editable { get; set; }
    public string? Etag { get; set; }
    public string? HtmlLink { get; set; }
}
public class GoogleEventRequest
{
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset? EndTime { get; set; }
    public bool IsAllDay { get; set; }
    public string? StartDate { get; set; }
    public string? EndDate { get; set; }
    public string? Location { get; set; }
    public string? Etag { get; set; }
}
public record GoogleCalendarSettings(List<string> CalendarIds, bool ShareDetailsWithCoach);
