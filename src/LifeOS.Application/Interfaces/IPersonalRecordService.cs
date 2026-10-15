using LifeOS.Application.DTOs.Personal;
namespace LifeOS.Application.Interfaces;
public interface IPersonalRecordService
{
    Task<List<JournalEntryDto>> JournalAsync(Guid userId, CancellationToken ct);
    Task<JournalEntryDto?> SaveJournalAsync(Guid userId, Guid? id, CreateJournalEntryRequest request, CancellationToken ct);
    Task<bool> DeleteJournalAsync(Guid userId, Guid id, CancellationToken ct);
    Task<RecipeDto?> SaveRecipeAsync(Guid userId, Guid? id, SaveRecipeRequest request, CancellationToken ct);
    Task<bool> DeleteRecipeAsync(Guid userId, Guid id, CancellationToken ct);
}
public class RecordConflictException(string message) : Exception(message);
