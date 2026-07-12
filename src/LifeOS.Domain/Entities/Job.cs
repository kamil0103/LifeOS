namespace LifeOS.Domain.Entities;

public class Job : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public string Title { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public string? Location { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? Url { get; set; }
    public string Source { get; set; } = "manual";
    public string? SalaryRange { get; set; }
    public string? JobType { get; set; }
    public DateTime? PostedDate { get; set; }
    public int? MatchScore { get; set; }
    public string Status { get; set; } = "saved";

    public List<JobApplication> Applications { get; set; } = [];
}
