using LifeOS.Application.DTOs.Coach;
namespace LifeOS.Application.Interfaces;
public interface IAiCoachService
{
    Task<CoachContext> ContextAsync(Guid userId,CancellationToken ct);
    Task<CoachPreferences> PreferencesAsync(Guid userId,CancellationToken ct);
    Task SavePreferencesAsync(Guid userId,CoachPreferences request,CancellationToken ct);
    Task<List<CoachMemoryDto>> MemoriesAsync(Guid userId,CancellationToken ct);
    Task<CoachMemoryDto?> SaveMemoryAsync(Guid userId,Guid? id,CoachMemoryRequest request,CancellationToken ct);
    Task<bool> DeleteMemoryAsync(Guid userId,Guid id,CancellationToken ct);
    Task<List<CoachHistoryItem>> HistoryAsync(Guid userId,CancellationToken ct);
    Task ClearHistoryAsync(Guid userId,CancellationToken ct);
    Task<CoachChatReply> ChatAsync(Guid userId,CoachChatRequest request,CancellationToken ct);
    Task<DailyMissionDto?> MissionAsync(Guid userId,CancellationToken ct);
    Task<DailyMissionDto> GenerateMissionAsync(Guid userId,CancellationToken ct);
    Task<DailyMissionDto?> CompleteMissionAsync(Guid userId,Guid id,CancellationToken ct);
    Task<List<AiCoachMessageDto>> InsightsAsync(Guid userId,int limit,CancellationToken ct);
    Task<bool> ReadInsightAsync(Guid userId,Guid id,CancellationToken ct);
}
