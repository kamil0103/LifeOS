namespace LifeOS.Domain.Entities;

public class DailyMission : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public DateTimeOffset MissionDate { get; set; }
    public string PrioritiesJson { get; set; } = "[]";
    public string? AiSummary { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}
