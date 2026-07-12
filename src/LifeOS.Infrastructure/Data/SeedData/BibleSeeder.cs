using System.Text.Json;
using LifeOS.Domain.Entities;
using LifeOS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Infrastructure.Data.SeedData;

public class BibleSeeder
{
    private readonly AppDbContext _context;

    public BibleSeeder(AppDbContext context)
    {
        _context = context;
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (await _context.BibleBooks.AnyAsync(ct))
            return; // Already seeded

        var assembly = typeof(BibleSeeder).Assembly;
        var resourceName = "LifeOS.Infrastructure.Data.SeedData.web_bible.json";
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
            return;

        using var reader = new StreamReader(stream);
        var json = await reader.ReadToEndAsync(ct);
        var data = JsonSerializer.Deserialize<BibleSeedData>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (data == null) return;

        var bookMap = new Dictionary<string, BibleBook>();
        foreach (var b in data.Books)
        {
            var book = new BibleBook
            {
                Name = b.Name,
                Abbreviation = b.Abbreviation,
                Testament = b.Testament,
                BookOrder = b.Order,
                ChapterCount = b.Chapters
            };
            _context.BibleBooks.Add(book);
            bookMap[b.Name] = book;
        }

        await _context.SaveChangesAsync(ct);

        foreach (var v in data.Verses)
        {
            if (!bookMap.TryGetValue(v.Book, out var book)) continue;
            _context.BibleVerses.Add(new BibleVerse
            {
                BookId = book.Id,
                Chapter = v.Chapter,
                VerseNumber = v.Verse,
                Text = v.Text
            });
        }

        await _context.SaveChangesAsync(ct);
    }

    private class BibleSeedData
    {
        public List<BookSeed> Books { get; set; } = [];
        public List<VerseSeed> Verses { get; set; } = [];
    }

    private class BookSeed
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Abbreviation { get; set; } = string.Empty;
        public string Testament { get; set; } = string.Empty;
        public int Order { get; set; }
        public int Chapters { get; set; }
    }

    private class VerseSeed
    {
        public string Book { get; set; } = string.Empty;
        public int Chapter { get; set; }
        public int Verse { get; set; }
        public string Text { get; set; } = string.Empty;
    }
}
