namespace LifeOS.Domain.Entities;
public class ResumeDraft
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid Revision { get; set; } = Guid.NewGuid();
    public string ContentJson { get; set; } = "{}";
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
