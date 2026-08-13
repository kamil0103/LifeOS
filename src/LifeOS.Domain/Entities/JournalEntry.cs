namespace LifeOS.Domain.Entities;

public class JournalEntry : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTimeOffset EntryDate { get; set; } = DateTimeOffset.UtcNow;
    public string? Mood { get; set; }
}
