namespace LifeOS.Domain.Entities;

public class ApplicationStatusHistory : BaseEntity
{
    public Guid ApplicationId { get; set; }
    public JobApplication Application { get; set; } = null!;

    public string Status { get; set; } = string.Empty;
    public DateTimeOffset ChangedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? Notes { get; set; }
}
