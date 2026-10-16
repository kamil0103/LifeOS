using LifeOS.Application.DTOs.Jobs;
namespace LifeOS.Application.Interfaces;
public interface ICompanyResearchService
{
    Task<List<CompanyNoteDto>> ListAsync(Guid userId, CancellationToken ct);
    Task<CompanyNoteDto?> SaveAsync(Guid userId, Guid? id, CompanyResearchRequest request, CancellationToken ct);
    Task<List<CompanyRelatedJobDto>?> RelatedJobsAsync(Guid userId, Guid id, CancellationToken ct);
    Task<bool> DeleteAsync(Guid userId, Guid id, CancellationToken ct);
}
