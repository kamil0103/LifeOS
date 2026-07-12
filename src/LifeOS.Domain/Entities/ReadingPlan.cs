namespace LifeOS.Domain.Entities;

public class ReadingPlan : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int DurationDays { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset? StartedAt { get; set; }

    public List<ReadingPlanDay> Days { get; set; } = [];
}
