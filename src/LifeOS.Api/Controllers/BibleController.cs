using LifeOS.Application.DTOs.Bible;
using LifeOS.Domain.Entities;
using LifeOS.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BibleController : ControllerBase
{
    private readonly AppDbContext _context;

    public BibleController(AppDbContext context)
    {
        _context = context;
    }

    private Guid GetUserId()
    {
        var sub = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        return Guid.Parse(sub!);
    }

    // ==================== BOOKS ====================

    [HttpGet("books")]
    public async Task<ActionResult<List<BibleBookDto>>> GetBooks(CancellationToken ct)
    {
        var books = await _context.BibleBooks
            .AsNoTracking()
            .OrderBy(b => b.BookOrder)
            .Select(b => new BibleBookDto
            {
                Id = b.Id,
                Name = b.Name,
                Abbreviation = b.Abbreviation,
                Testament = b.Testament,
                BookOrder = b.BookOrder,
                ChapterCount = b.ChapterCount
            })
            .ToListAsync(ct);

        return books;
    }

    // ==================== CHAPTER ====================

    [HttpGet("books/{bookId}/chapters/{chapter}")]
    public async Task<ActionResult<ChapterDto>> GetChapter(Guid bookId, int chapter, CancellationToken ct)
    {
        var userId = GetUserId();
        var book = await _context.BibleBooks.AsNoTracking().FirstOrDefaultAsync(b => b.Id == bookId, ct);
        if (book == null) return NotFound();

        var verses = await _context.BibleVerses
            .AsNoTracking()
            .Where(v => v.BookId == bookId && v.Chapter == chapter)
            .OrderBy(v => v.VerseNumber)
            .ToListAsync(ct);

        var bookmarkedVerseIds = await _context.Bookmarks
            .AsNoTracking()
            .Where(b => b.UserId == userId)
            .Select(b => b.VerseId)
            .ToListAsync(ct);

        var bookmarkMap = await _context.Bookmarks
            .AsNoTracking()
            .Where(b => b.UserId == userId && bookmarkedVerseIds.Contains(b.VerseId))
            .ToDictionaryAsync(b => b.VerseId, b => b, ct);

        return new ChapterDto
        {
            BookId = bookId,
            BookName = book.Name,
            Chapter = chapter,
            Verses = verses.Select(v => new BibleVerseDto
            {
                Id = v.Id,
                BookId = v.BookId,
                BookName = book.Name,
                Chapter = v.Chapter,
                VerseNumber = v.VerseNumber,
                Text = v.Text,
                IsBookmarked = bookmarkedVerseIds.Contains(v.Id),
                BookmarkColor = bookmarkMap.ContainsKey(v.Id) ? bookmarkMap[v.Id].Color : null,
                BookmarkNote = bookmarkMap.ContainsKey(v.Id) ? bookmarkMap[v.Id].Note : null
            }).ToList()
        };
    }

    // ==================== SEARCH ====================

    [HttpGet("search")]
    public async Task<ActionResult<List<SearchResultDto>>> Search([FromQuery] string q, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
            return BadRequest(new ProblemDetails { Title = "Query too short", Detail = "Search query must be at least 2 characters." });

        var lowerQ = q.ToLower();
        var results = await _context.BibleVerses
            .AsNoTracking()
            .Where(v => v.Text.ToLower().Contains(lowerQ))
            .Include(v => v.Book)
            .OrderBy(v => v.Book.BookOrder)
            .ThenBy(v => v.Chapter)
            .ThenBy(v => v.VerseNumber)
            .Take(50)
            .Select(v => new SearchResultDto
            {
                Id = v.Id,
                BookName = v.Book.Name,
                Reference = $"{v.Book.Name} {v.Chapter}:{v.VerseNumber}",
                Text = v.Text,
                Chapter = v.Chapter,
                VerseNumber = v.VerseNumber
            })
            .ToListAsync(ct);

        return results;
    }

    // ==================== DAILY VERSE ====================

    [HttpGet("daily")]
    public async Task<ActionResult<DailyVerseDto>> GetDailyVerse(CancellationToken ct)
    {
        var userId = GetUserId();
        var today = DateTimeOffset.UtcNow.Date;
        var dayOfYear = today.DayOfYear;

        // Deterministic daily verse based on day of year
        var count = await _context.BibleVerses.CountAsync(ct);
        if (count == 0)
            return NotFound(new ProblemDetails { Title = "No verses", Detail = "Bible data has not been seeded yet." });

        var index = dayOfYear % count;
        var verse = await _context.BibleVerses
            .AsNoTracking()
            .Include(v => v.Book)
            .Skip(index)
            .Take(1)
            .Select(v => new DailyVerseDto
            {
                Id = v.Id,
                Reference = $"{v.Book.Name} {v.Chapter}:{v.VerseNumber}",
                Text = v.Text
            })
            .FirstOrDefaultAsync(ct);

        if (verse == null)
            return NotFound();
        return verse;
    }

    // ==================== BOOKMARKS ====================

    [HttpGet("bookmarks")]
    public async Task<ActionResult<List<BookmarkDto>>> GetBookmarks(CancellationToken ct)
    {
        var userId = GetUserId();
        var bookmarks = await _context.Bookmarks
            .AsNoTracking()
            .Where(b => b.UserId == userId)
            .Include(b => b.Verse).ThenInclude(v => v.Book)
            .OrderByDescending(b => b.CreatedAt)
            .Select(b => new BookmarkDto
            {
                Id = b.Id,
                VerseId = b.VerseId,
                Reference = $"{b.Verse.Book.Name} {b.Verse.Chapter}:{b.Verse.VerseNumber}",
                Text = b.Verse.Text,
                Note = b.Note,
                Color = b.Color,
                CreatedAt = b.CreatedAt
            })
            .ToListAsync(ct);

        return bookmarks;
    }

    [HttpPost("bookmarks")]
    public async Task<ActionResult<BookmarkDto>> CreateBookmark([FromBody] CreateBookmarkRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        var verse = await _context.BibleVerses.AsNoTracking().FirstOrDefaultAsync(v => v.Id == request.VerseId, ct);
        if (verse == null)
            return BadRequest(new ProblemDetails { Title = "Verse not found", Detail = "The specified verse does not exist." });

        var existing = await _context.Bookmarks.FirstOrDefaultAsync(b => b.UserId == userId && b.VerseId == request.VerseId, ct);
        if (existing != null)
        {
            existing.Note = request.Note;
            existing.Color = request.Color;
            await _context.SaveChangesAsync(ct);
            return new BookmarkDto
            {
                Id = existing.Id,
                VerseId = existing.VerseId,
                Reference = $"{verse.Book.Name} {verse.Chapter}:{verse.VerseNumber}",
                Text = verse.Text,
                Note = existing.Note,
                Color = existing.Color,
                CreatedAt = existing.CreatedAt
            };
        }

        var bookmark = new Bookmark
        {
            UserId = userId,
            VerseId = request.VerseId,
            Note = request.Note,
            Color = request.Color
        };
        _context.Bookmarks.Add(bookmark);
        await _context.SaveChangesAsync(ct);

        return new BookmarkDto
        {
            Id = bookmark.Id,
            VerseId = bookmark.VerseId,
            Reference = $"{verse.Book.Name} {verse.Chapter}:{verse.VerseNumber}",
            Text = verse.Text,
            Note = bookmark.Note,
            Color = bookmark.Color,
            CreatedAt = bookmark.CreatedAt
        };
    }

    [HttpDelete("bookmarks/{id}")]
    public async Task<IActionResult> DeleteBookmark(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        var bookmark = await _context.Bookmarks.FirstOrDefaultAsync(b => b.Id == id && b.UserId == userId, ct);
        if (bookmark == null)
            return NotFound();

        _context.Bookmarks.Remove(bookmark);
        await _context.SaveChangesAsync(ct);
        return NoContent();
    }
}
