using LifeOS.Application.DTOs.Habits;
namespace LifeOS.Application.Interfaces;
public interface IHabitService
{
    Task<List<HabitDto>> ListAsync(Guid userId, bool archived, bool dueOnly, CancellationToken ct);
    Task<HabitDto?> SaveAsync(Guid userId, Guid? id, HabitSaveRequest request, CancellationToken ct);
    Task<bool> SetActiveAsync(Guid userId, Guid id, bool active, CancellationToken ct);
    Task<HabitCompletionResult?> ProgressAsync(Guid userId, Guid id, HabitCompletionRequest? request, bool completeRoute, CancellationToken ct);
    Task<List<HabitHistoryDto>?> HistoryAsync(Guid userId, Guid id, int days, CancellationToken ct);
    Task<List<StreakDto>> StreaksAsync(Guid userId, CancellationToken ct);
}
