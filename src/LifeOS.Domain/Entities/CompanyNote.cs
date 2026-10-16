namespace LifeOS.Domain.Entities;

public class CompanyNote : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public string CompanyName { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string? Website { get; set; }
    public string? Industry { get; set; }
    public string? Products { get; set; }
    public string? TechnologyStack { get; set; }
    public string? Culture { get; set; }
    public string? InterviewPreparation { get; set; }
    public int? Rating { get; set; }
}
