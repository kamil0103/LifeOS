namespace LifeOS.Domain.Entities;

public class JobApplication : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public Guid JobId { get; set; }
    public Job Job { get; set; } = null!;

    public Guid? ResumeVersionId { get; set; }
    public ResumeVersion? ResumeVersion { get; set; }

    public Guid? CoverLetterDocumentId { get; set; }
    public Document? CoverLetterDocument { get; set; }

    public string Status { get; set; } = "applied";
    public DateTime? AppliedDate { get; set; }
    public DateTime? FollowUpDate { get; set; }
    public string? Notes { get; set; }

    public List<ApplicationStatusHistory> StatusHistory { get; set; } = [];
}
