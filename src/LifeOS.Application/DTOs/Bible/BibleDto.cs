namespace LifeOS.Application.DTOs.Bible;

public class BibleBookDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Abbreviation { get; set; } = string.Empty;
    public string Testament { get; set; } = string.Empty;
    public int BookOrder { get; set; }
    public int ChapterCount { get; set; }
}

public class BibleVerseDto
{
    public Guid Id { get; set; }
    public Guid BookId { get; set; }
    public string BookName { get; set; } = string.Empty;
    public int Chapter { get; set; }
    public int VerseNumber { get; set; }
    public string Text { get; set; } = string.Empty;
    public bool IsBookmarked { get; set; }
    public string? BookmarkColor { get; set; }
    public string? BookmarkNote { get; set; }
}

public class ChapterDto
{
    public Guid BookId { get; set; }
    public string BookName { get; set; } = string.Empty;
    public int Chapter { get; set; }
    public List<BibleVerseDto> Verses { get; set; } = new();
}

public class SearchResultDto
{
    public Guid Id { get; set; }
    public string BookName { get; set; } = string.Empty;
    public string Reference { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public int Chapter { get; set; }
    public int VerseNumber { get; set; }
}

public class DailyVerseDto
{
    public Guid Id { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
}

public class BookmarkDto
{
    public Guid Id { get; set; }
    public Guid VerseId { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string? Note { get; set; }
    public string Color { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}

public class CreateBookmarkRequest
{
    public Guid VerseId { get; set; }
    public string? Note { get; set; }
    public string Color { get; set; } = "yellow";
}
