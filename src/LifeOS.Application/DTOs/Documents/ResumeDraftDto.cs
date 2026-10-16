using System.ComponentModel.DataAnnotations;
namespace LifeOS.Application.DTOs.Documents;
public record ResumeDraftDto(Guid Revision, DateTimeOffset UpdatedAt, ResumeDataDto Data);
public class SaveResumeDraftRequest
{
    public Guid? ExpectedRevision { get; set; }
    [Required] public ResumeDataDto Data { get; set; } = new();
}
