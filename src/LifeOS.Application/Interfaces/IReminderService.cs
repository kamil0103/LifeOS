using LifeOS.Application.DTOs.Planning;
using LifeOS.Domain.Entities;
namespace LifeOS.Application.Interfaces;
public interface IReminderService
{
    Task RemindAsync(Guid userId,DailyPlanDto plan,CancellationToken ct);
    Task<ReminderPreferencesDto> PreferencesAsync(Guid userId,CancellationToken ct);
    Task SavePreferencesAsync(Guid userId,ReminderPreferencesDto request,CancellationToken ct);
    Task<List<Notification>> ListAsync(Guid userId,bool unread,int page,CancellationToken ct);
    Task<int> CountAsync(Guid userId,CancellationToken ct);
    Task<bool> ReadAsync(Guid userId,Guid id,CancellationToken ct);
    Task ReadAllAsync(Guid userId,CancellationToken ct);
    Task<bool> ArchiveAsync(Guid userId,Guid id,CancellationToken ct);
    Task<bool> SnoozeAsync(Guid userId,Guid id,int minutes,CancellationToken ct);
}
