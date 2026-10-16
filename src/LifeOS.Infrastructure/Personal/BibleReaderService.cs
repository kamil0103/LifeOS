using System.Security.Cryptography;
using System.Text;
using LifeOS.Application.DTOs.Bible;
using LifeOS.Application.Interfaces;
using LifeOS.Domain.Entities;
using LifeOS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
namespace LifeOS.Infrastructure.Personal;
public class BibleReaderService(AppDbContext db, ILocalDayService days) : IBibleReaderService
{
    public async Task<DailyVerseDto?> DailyAsync(Guid userId, bool random, CancellationToken ct)
    {
        var count = await db.BibleVerses.CountAsync(ct); if (count == 0) return null;
        var date = (await days.TodayAsync(userId, ct)).Date;
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(userId.ToString("N") + date.ToString("yyyy-MM-dd")));
        var index = random ? RandomNumberGenerator.GetInt32(count) : (int)(BitConverter.ToUInt32(hash, 0) % (uint)count);
        return await db.BibleVerses.AsNoTracking().OrderBy(v => v.Book.BookOrder).ThenBy(v => v.Chapter).ThenBy(v => v.VerseNumber).ThenBy(v => v.Id).Skip(index)
            .Select(v => new DailyVerseDto { Id = v.Id, Reference = v.Book.Name + " " + v.Chapter + ":" + v.VerseNumber, Text = v.Text }).FirstOrDefaultAsync(ct);
    }
    public Task<BookmarkDto> BookmarkAsync(Guid userId, CreateBookmarkRequest request, CancellationToken ct)
    {
        if ((request.Note?.Length ?? 0) > 5000 || !new[] { "yellow", "blue", "green", "red", "purple" }.Contains(request.Color)) throw new InvalidOperationException("Use a listed bookmark color and a note of at most 5000 characters.");
        return PersonalWriteLock.RunAsync(db, userId, async () => {
            var verse = await db.BibleVerses.AsNoTracking().Include(v => v.Book).FirstOrDefaultAsync(v => v.Id == request.VerseId, ct) ?? throw new InvalidOperationException("The selected verse does not exist.");
            var bookmark = await db.Bookmarks.Where(b => b.UserId == userId && b.VerseId == request.VerseId).OrderByDescending(b => b.UpdatedAt).ThenBy(b => b.Id).FirstOrDefaultAsync(ct);
            if (bookmark == null) { bookmark = new() { UserId = userId, VerseId = request.VerseId }; db.Bookmarks.Add(bookmark); }
            bookmark.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(); bookmark.Color = request.Color; bookmark.UpdatedAt = DateTimeOffset.UtcNow;
            return new BookmarkDto { Id = bookmark.Id, VerseId = verse.Id, BookId = verse.BookId, Chapter = verse.Chapter, Reference = verse.Book.Name + " " + verse.Chapter + ":" + verse.VerseNumber,
                Text = verse.Text, Note = bookmark.Note, Color = bookmark.Color, CreatedAt = bookmark.CreatedAt };
        }, ct);
    }
    public Task<bool> DeleteBookmarkAsync(Guid userId, Guid id, CancellationToken ct) => PersonalWriteLock.RunAsync(db, userId, async () => {
        var bookmark = await db.Bookmarks.FirstOrDefaultAsync(b => b.UserId == userId && b.Id == id, ct); if (bookmark == null) return false; db.Bookmarks.Remove(bookmark); return true;
    }, ct);
}
