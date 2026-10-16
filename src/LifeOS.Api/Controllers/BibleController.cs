using LifeOS.Application.DTOs.Bible;
using LifeOS.Domain.Entities;
using LifeOS.Application.Interfaces;
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
    private readonly IBibleReaderService _reader;

    public BibleController(AppDbContext context, IBibleReaderService reader)
    {
        _context = context;
        _reader = reader;
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
        if (chapter < 1 || chapter > book.ChapterCount) return BadRequest(new ProblemDetails { Detail = "Select a chapter in this book." });

        var verses = await _context.BibleVerses
            .AsNoTracking()
            .Where(v => v.BookId == bookId && v.Chapter == chapter)
            .OrderBy(v => v.VerseNumber)
            .ToListAsync(ct);

        var verseIds = verses.Select(v => v.Id).ToList();
        var bookmarks = await _context.Bookmarks.AsNoTracking().Where(b => b.UserId == userId && verseIds.Contains(b.VerseId)).ToListAsync(ct);
        var bookmarkMap = bookmarks.GroupBy(b => b.VerseId).ToDictionary(g => g.Key, g => g.OrderByDescending(b => b.UpdatedAt).ThenBy(b => b.Id).First());
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
                IsBookmarked = bookmarkMap.ContainsKey(v.Id),
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
                BookId = v.BookId,
                Reference = $"{v.Book.Name} {v.Chapter}:{v.VerseNumber}",
                Text = v.Text,
                Chapter = v.Chapter,
                VerseNumber = v.VerseNumber
            })
            .ToListAsync(ct);

        return results;
    }

    [HttpGet("daily")]
    public async Task<ActionResult<DailyVerseDto>> GetDailyVerse(CancellationToken ct)
        => await _reader.DailyAsync(GetUserId(), false, ct) is { } result ? Ok(result) : NotFound();

    [HttpPost("daily/refresh")]
    public async Task<ActionResult<DailyVerseDto>> RefreshDailyVerse(CancellationToken ct)
        => await _reader.DailyAsync(GetUserId(), true, ct) is { } result ? Ok(result) : NotFound();

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
                BookId = b.Verse.BookId,
                Chapter = b.Verse.Chapter,
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
    public async Task<ActionResult<BookmarkDto>> CreateBookmark(CreateBookmarkRequest request, CancellationToken ct)
        => await _reader.BookmarkAsync(GetUserId(), request, ct);

    [HttpDelete("bookmarks/{id:guid}")]
    public async Task<IActionResult> DeleteBookmark(Guid id, CancellationToken ct)
        => await _reader.DeleteBookmarkAsync(GetUserId(), id, ct) ? NoContent() : NotFound();
}
