using LifeOS.Application.DTOs.Calendar;
namespace LifeOS.Application.Interfaces;
public interface IGoogleCalendarService
{
    Task<GoogleCalendarStatus> StatusAsync(Guid userId,CancellationToken ct);
    string ConnectUrl(Guid userId);
    Task CompleteConnectionAsync(string state,string code,CancellationToken ct);
    string ReturnUrl { get; }
    Task DisconnectAsync(Guid userId,CancellationToken ct);
    Task SettingsAsync(Guid userId,GoogleCalendarSettings settings,CancellationToken ct);
    Task<List<GoogleEventDto>> EventsAsync(Guid userId,DateTimeOffset start,DateTimeOffset end,bool refresh,CancellationToken ct);
    Task<GoogleEventDto> CreateAsync(Guid userId,GoogleEventRequest request,CancellationToken ct);
    Task<GoogleEventDto> UpdateAsync(Guid userId,string eventId,GoogleEventRequest request,CancellationToken ct);
    Task DeleteAsync(Guid userId,string eventId,string etag,CancellationToken ct);
    Task SyncAllAsync(CancellationToken ct);
}
public class CalendarConflictException : Exception { public CalendarConflictException() : base("This event changed in Google Calendar. Reload it before saving your changes.") {} }
