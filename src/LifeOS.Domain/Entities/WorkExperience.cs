namespace LifeOS.Domain.Entities;

public class WorkExperience : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public string Company { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Location { get; set; }
    public string? StartDate { get; set; }
    public string? EndDate { get; set; }
    public bool IsCurrent { get; set; } = false;
    public string? Bullets { get; set; }
}
