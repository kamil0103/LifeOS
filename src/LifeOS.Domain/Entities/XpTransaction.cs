namespace LifeOS.Domain.Entities;

public class XpTransaction : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public int Amount { get; set; }
    public string Source { get; set; } = string.Empty; // habit, job_application, coding, bible, ai_coach
    public Guid? SourceId { get; set; }
    public string? Description { get; set; }
}
