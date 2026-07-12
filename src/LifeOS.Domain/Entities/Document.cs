namespace LifeOS.Domain.Entities;

public class Document : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public Guid? JobId { get; set; }
    public Job? Job { get; set; }

    public string Type { get; set; } = string.Empty; // resume, cover_letter, qa_sheet
    public string Filename { get; set; } = string.Empty;
    public string StoragePath { get; set; } = string.Empty;
    public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.UtcNow;
}
