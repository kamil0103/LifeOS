namespace LifeOS.Domain.Entities;

public class Bookmark : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public Guid VerseId { get; set; }
    public BibleVerse Verse { get; set; } = null!;

    public string? Note { get; set; }
    public string Color { get; set; } = "yellow";
    public new DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
