using FluentValidation;
using LifeOS.Application.DTOs.Experience;
using LifeOS.Application.DTOs.Portfolio;
namespace LifeOS.Application.DTOs.Skills;
public class SkillRules<T> : AbstractValidator<T> where T : CreateSkillRequest
{
    public SkillRules() { RuleFor(r => r.Name).NotEmpty().MaximumLength(300); RuleFor(r => r.Category).NotEmpty().MaximumLength(300); RuleFor(r => r.Source).MaximumLength(500); RuleFor(r => r.Proficiency).NotEmpty(); }
}
public class CreateSkillRequestValidator : SkillRules<CreateSkillRequest> { }
public class UpdateSkillRequestValidator : SkillRules<UpdateSkillRequest> { }
public class CertificateRules<T> : AbstractValidator<T> where T : CreateCertificateRequest
{
    public CertificateRules() { RuleFor(r => r.Name).NotEmpty().MaximumLength(300); RuleFor(r => r.Issuer).MaximumLength(300); RuleFor(r => r.CredentialId).MaximumLength(300); RuleFor(r => r.Description).MaximumLength(10000); RuleFor(r => r.Url).MaximumLength(2048); }
}
public class CreateCertificateRequestValidator : CertificateRules<CreateCertificateRequest> { }
public class UpdateCertificateRequestValidator : CertificateRules<UpdateCertificateRequest> { }
public class WorkRules<T> : AbstractValidator<T> where T : CreateWorkExperienceRequest
{
    public WorkRules() { RuleFor(r => r.Company).NotEmpty().MaximumLength(300); RuleFor(r => r.Title).NotEmpty().MaximumLength(300); RuleFor(r => r.Location).MaximumLength(500); RuleFor(r => r.Bullets).MaximumLength(20000); }
}
public class CreateWorkExperienceRequestValidator : WorkRules<CreateWorkExperienceRequest> { }
public class UpdateWorkExperienceRequestValidator : WorkRules<UpdateWorkExperienceRequest> { }
public class ProjectRules<T> : AbstractValidator<T> where T : CreateProjectRequest
{
    public ProjectRules() { RuleFor(r => r.Name).NotEmpty().MaximumLength(300); RuleFor(r => r.Description).MaximumLength(20000); RuleFor(r => r.Technologies).MaximumLength(2000); RuleFor(r => r.Link).MaximumLength(2048); }
}
public class CreateProjectRequestValidator : ProjectRules<CreateProjectRequest> { }
public class UpdateProjectRequestValidator : ProjectRules<UpdateProjectRequest> { }
public class SavePortfolioProjectRequestValidator : ProjectRules<SavePortfolioProjectRequest> { }
public class ImportGitHubProjectRequestValidator : AbstractValidator<ImportGitHubProjectRequest>
{
    public ImportGitHubProjectRequestValidator() { RuleFor(r => r.RepoUrl).NotEmpty().MaximumLength(2048); }
}
