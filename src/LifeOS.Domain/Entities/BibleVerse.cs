namespace LifeOS.Domain.Entities;

public class BibleVerse : BaseEntity
{
    public Guid BookId { get; set; }
    public BibleBook Book { get; set; } = null!;

    public int Chapter { get; set; }
    public int VerseNumber { get; set; }
    public string Text { get; set; } = string.Empty;
}
