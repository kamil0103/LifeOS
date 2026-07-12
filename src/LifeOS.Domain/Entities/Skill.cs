namespace LifeOS.Domain.Entities;

public class Skill : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = "Other";
    public string Proficiency { get; set; } = "Beginner";
    public string? Source { get; set; }
}
