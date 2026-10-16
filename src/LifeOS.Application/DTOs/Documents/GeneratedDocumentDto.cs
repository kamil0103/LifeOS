namespace LifeOS.Application.DTOs.Documents;

public class GeneratedDocumentDto
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty; // resume, cover_letter, qa_sheet
    public string Filename { get; set; } = string.Empty;
    public DateTimeOffset GeneratedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid? ApplicationId { get; set; }
    public string? ApplicationCompany { get; set; }
    public string? ApplicationStatus { get; set; }
    public Guid? SourceResumeVersionId { get; set; }
    public string? SourceResumeVersionTitle { get; set; }
    public Guid? JobId { get; set; }
    public string? JobTitle { get; set; }
}

public class ResumeVersionDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Template { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public ResumeDataDto? Data { get; set; }
}

public class SaveResumeVersionRequest
{
    public string Title { get; set; } = "My Resume";
    public string Template { get; set; } = "modern";
    public ResumeDataDto Data { get; set; } = new();
}

public class DocumentLinksRequest
{
    public Guid? JobId { get; set; }
    public Guid? ApplicationId { get; set; }
    public Guid? SourceResumeVersionId { get; set; }
    public DateTimeOffset? ExpectedUpdatedAt { get; set; }
}
public record DocumentLinkTargets(Guid? JobId, Guid? ApplicationId, Guid? SourceResumeVersionId);
