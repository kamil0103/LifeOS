namespace LifeOS.Application.DTOs.Jobs;

public class JobApplicationDto
{
    public Guid Id { get; set; }
    public Guid JobId { get; set; }
    public string JobTitle { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? AppliedDate { get; set; }
    public DateTime? FollowUpDate { get; set; }
    public string? Notes { get; set; }
    public Guid? ResumeVersionId { get; set; }
    public string? ResumeVersionTitle { get; set; }
    public List<ApplicationStatusHistoryDto> StatusHistory { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; }
}

public class ApplicationStatusHistoryDto
{
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset ChangedAt { get; set; }
    public string? Notes { get; set; }
}

public class CreateApplicationRequest
{
    public Guid JobId { get; set; }
    public Guid? ResumeVersionId { get; set; }
    public DateTime? AppliedDate { get; set; }
    public string? Notes { get; set; }
}

public class UpdateApplicationStatusRequest
{
    public string Status { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public class ApplicationStatsDto
{
    public int TotalSaved { get; set; }
    public int TotalApplied { get; set; }
    public int TotalPhoneScreen { get; set; }
    public int TotalInterview { get; set; }
    public int TotalOffer { get; set; }
    public int TotalRejected { get; set; }
}
