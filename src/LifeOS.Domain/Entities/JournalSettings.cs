namespace LifeOS.Domain.Entities;

public class JournalSettings : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public string? GoogleDocId { get; set; }
    public string? ServiceAccountJson { get; set; }
    public bool AutoSync { get; set; }
    public DateTimeOffset? LastSyncAt { get; set; }
}
