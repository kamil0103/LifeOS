using LifeOS.Application.DTOs.Skills;
namespace LifeOS.Application.Interfaces;
public interface ISkillEvidenceService
{
    Task<List<EvidenceSourceDto>> SourcesAsync(Guid userId,CancellationToken ct);
    Task<List<SkillEvidenceDto>?> ListAsync(Guid userId,Guid skillId,CancellationToken ct);
    Task<SkillEvidenceDto?> AddAsync(Guid userId,Guid skillId,SaveSkillEvidenceRequest request,CancellationToken ct);
    Task<bool> RemoveAsync(Guid userId,Guid skillId,Guid evidenceId,CancellationToken ct);
}
