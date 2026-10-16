using System.ComponentModel.DataAnnotations;
namespace LifeOS.Application.DTOs.Jobs;

public class CompanyNoteDto
{
    public Guid Id { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public int? Rating { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string? Website { get; set; }
    public string? Industry { get; set; }
    public string? Products { get; set; }
    public string? TechnologyStack { get; set; }
    public string? Culture { get; set; }
    public string? InterviewPreparation { get; set; }
}
public class CompanyResearchRequest
{
    [Required, StringLength(200)] public string CompanyName { get; set; } = string.Empty;
    [StringLength(20000)] public string? Notes { get; set; }
    [Range(1,5)] public int? Rating { get; set; }
    public DateTimeOffset? ExpectedUpdatedAt { get; set; }
    [StringLength(2048)] public string? Website { get; set; }
    [StringLength(200)] public string? Industry { get; set; }
    [StringLength(10000)] public string? Products { get; set; }
    [StringLength(10000)] public string? TechnologyStack { get; set; }
    [StringLength(10000)] public string? Culture { get; set; }
    [StringLength(10000)] public string? InterviewPreparation { get; set; }
}
public class CreateCompanyNoteRequest : CompanyResearchRequest { }
public class UpdateCompanyNoteRequest : CompanyResearchRequest { }
public class CompanyRelatedJobDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Location { get; set; }
    public string? SalaryRange { get; set; }
    public string Status { get; set; } = string.Empty;
    public Guid? ApplicationId { get; set; }
    public string? ApplicationStatus { get; set; }
}
