using LifeOS.Application.DTOs.Skills;
using LifeOS.Application.DTOs.Experience;
using LifeOS.Application.DTOs.Portfolio;
namespace LifeOS.Application.Interfaces;
public interface IProfessionalRecordService
{
    Task<SkillDto?> SaveSkillAsync(Guid userId, Guid? id, CreateSkillRequest request, CancellationToken ct);
    Task SaveExtractedAsync(Guid userId, List<ExtractedSkillDto> skills, CancellationToken ct);
    Task<List<CertificateDto>> CertificatesAsync(Guid userId, CancellationToken ct);
    Task<CertificateDto?> SaveCertificateAsync(Guid userId, Guid? id, CreateCertificateRequest request, CancellationToken ct);
    Task<WorkExperienceDto?> SaveWorkAsync(Guid userId, Guid? id, CreateWorkExperienceRequest request, CancellationToken ct);
    Task<ProjectDto?> SaveProjectAsync(Guid userId, Guid? id, CreateProjectRequest request, CancellationToken ct);
    Task<PortfolioProjectDto?> SavePortfolioAsync(Guid userId, Guid id, SavePortfolioProjectRequest request, CancellationToken ct);
    Task<PortfolioProjectDto?> UpdatePortfolioAsync(Guid userId, Guid id, UpdatePortfolioProjectRequest request, CancellationToken ct);
    Task<PortfolioProjectDto> ImportAsync(Guid userId, string repoUrl, CancellationToken ct);
    Task<PortfolioProjectDto?> SyncAsync(Guid userId, Guid id, CancellationToken ct);
}
public interface IGitHubRepositoryClient
{
    Task<List<GitHubRepoInfoDto>> ListAsync(string username, CancellationToken ct);
    Task<GitHubRepoInfoDto> GetAsync(string repoUrl, CancellationToken ct);
}
