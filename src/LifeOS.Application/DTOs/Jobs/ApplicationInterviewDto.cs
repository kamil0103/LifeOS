using System.ComponentModel.DataAnnotations;
namespace LifeOS.Application.DTOs.Jobs;
public class SaveInterviewRequest
{
    public Guid RequestId { get; set; }
    public DateTimeOffset? ExpectedUpdatedAt { get; set; }
    [Required, StringLength(150)] public string Round { get; set; } = string.Empty;
    public string Format { get; set; } = "video";
    public string Status { get; set; } = "scheduled";
    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset EndTime { get; set; }
    [StringLength(300)] public string? Interviewer { get; set; }
    [StringLength(1000)] public string? Location { get; set; }
    [StringLength(2048)] public string? MeetingUrl { get; set; }
    [StringLength(10000)] public string? Preparation { get; set; }
    [StringLength(10000)] public string? Outcome { get; set; }
}
public class InterviewRevisionDto
{
    public string Round { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset EndTime { get; set; }
    public string? Interviewer { get; set; }
    public string? Location { get; set; }
    public string? MeetingUrl { get; set; }
    public string? Preparation { get; set; }
    public string? Outcome { get; set; }
    public DateTimeOffset ChangedAt { get; set; }
}
public class ApplicationInterviewDto : InterviewRevisionDto
{
    public Guid Id { get; set; }
    public Guid ApplicationId { get; set; }
    public Guid? CalendarEventId { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public List<InterviewRevisionDto> History { get; set; } = [];
}
