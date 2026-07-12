namespace LifeOS.Domain.Entities;

public class BibleBook : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Abbreviation { get; set; } = string.Empty;
    public string Testament { get; set; } = string.Empty; // OT or NT
    public int BookOrder { get; set; }
    public int ChapterCount { get; set; }

    public List<BibleVerse> Verses { get; set; } = [];
}
