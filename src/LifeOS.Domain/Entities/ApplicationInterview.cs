namespace LifeOS.Domain.Entities;
public class ApplicationInterview : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid ApplicationId { get; set; }
    public JobApplication Application { get; set; } = null!;
    public Guid RequestId { get; set; }
    public string CreationFingerprint { get; set; } = string.Empty;
    public string Round { get; set; } = string.Empty;
    public string Format { get; set; } = "video";
    public string Status { get; set; } = "scheduled";
    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset EndTime { get; set; }
    public string? Interviewer { get; set; }
    public string? Location { get; set; }
    public string? MeetingUrl { get; set; }
    public string? Preparation { get; set; }
    public string? Outcome { get; set; }
    public string HistoryJson { get; set; } = "[]";
}
