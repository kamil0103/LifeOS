using LifeOS.Application.DTOs.Calendar;
namespace LifeOS.Application.Interfaces;
public interface ICalendarService
{
    Task<List<CalendarEventDto>> GetAsync(Guid userId, DateTimeOffset? start, DateTimeOffset? end, CancellationToken ct);
    Task<CalendarEventDto> CreateAsync(Guid userId, CreateCalendarEventRequest request, CancellationToken ct);
    Task<CalendarEventDto?> UpdateAsync(Guid userId, Guid id, UpdateCalendarEventRequest request, CancellationToken ct);
    Task<bool> DeleteAsync(Guid userId, Guid id, CancellationToken ct);
}
