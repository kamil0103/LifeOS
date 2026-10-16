namespace LifeOS.Application.DTOs.Planning;
public record DailyActionDto(string Id, string Kind, Guid? SourceId, string Title, string Reason, int Priority, bool Completed, DateTimeOffset? DueAt, string ActionUrl, bool CanComplete);
public record CommitmentDto(string Id, string Title, DateTimeOffset StartTime, DateTimeOffset? EndTime, bool IsAllDay, string Source, string ActionUrl);
public class DailyPlanDto
{
    public string Date { get; set; } = "";
    public string TimeZone { get; set; } = "";
    public List<DailyActionDto> Actions { get; set; } = [];
    public List<CommitmentDto> Commitments { get; set; } = [];
    public string? GoogleWarning { get; set; }
}
public class MissionItem
{
    public string Title { get; set; } = "";
    public string Category { get; set; } = "";
    public string Priority { get; set; } = "";
    public string Reason { get; set; } = "";
    public bool Completed { get; set; }
}
public record ScheduleActionRequest(string ActionId, DateTimeOffset StartTime, DateTimeOffset EndTime, string Destination);
public record ScheduledActionResult(string EventId, string ActionUrl, bool AlreadyScheduled);
public record FollowUpRequest(DateTimeOffset? DueAt, string? Notes, DateTimeOffset? EndAt = null);
public record ApplicationDetailsRequest(string? RecruiterName, string? RecruiterEmail, string? RecruiterPhone, Guid? ResumeVersionId, Guid? CoverLetterDocumentId);
public record ReminderPreferencesDto(bool FollowUpsEnabled, bool CalendarEnabled, bool CodingReviewsEnabled, int CalendarLeadMinutes, bool? CertificateExpiryEnabled = null);
