using LifeOS.Application.DTOs.Bible;
namespace LifeOS.Application.Interfaces;
public interface IBibleReaderService
{
    Task<DailyVerseDto?> DailyAsync(Guid userId, bool random, CancellationToken ct);
    Task<BookmarkDto> BookmarkAsync(Guid userId, CreateBookmarkRequest request, CancellationToken ct);
    Task<bool> DeleteBookmarkAsync(Guid userId, Guid id, CancellationToken ct);
}
