namespace LifeOS.Domain.Entities;
public class SkillEvidence : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid SkillId { get; set; }
    public Skill Skill { get; set; } = null!;
    public string SourceType { get; set; } = "";
    public Guid SourceId { get; set; }
    public string? Notes { get; set; }
}
