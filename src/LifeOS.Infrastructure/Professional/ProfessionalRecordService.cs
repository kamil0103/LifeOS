using System.Collections.Concurrent;
using LifeOS.Application.DTOs.Skills;
using LifeOS.Application.DTOs.Experience;
using LifeOS.Application.DTOs.Portfolio;
using LifeOS.Application.Interfaces;
using LifeOS.Application.Services;
using LifeOS.Domain.Entities;
using LifeOS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using static LifeOS.Application.Services.ProfessionalRecordRules;
namespace LifeOS.Infrastructure.Professional;

public class ProfessionalRecordService(AppDbContext db, IGitHubRepositoryClient github, ILocalDayService days) : IProfessionalRecordService
{
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> Gates = new();
    private async Task<T> WriteAsync<T>(Guid userId, Func<Task<T>> action, CancellationToken ct)
    {
        var gate = Gates.GetOrAdd(userId, _ => new(1, 1)); await gate.WaitAsync(ct);
        try
        {
            await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
            if (transaction != null) { var key = BitConverter.ToInt64(userId.ToByteArray(), 0) ^ 0x50524F464553L; await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({key})", ct); }
            var result = await action(); await db.SaveChangesAsync(ct); if (transaction != null) await transaction.CommitAsync(ct); return result;
        }
        finally { gate.Release(); }
    }
    private static void ValidateSkill(CreateSkillRequest r)
    {
        Name(r.Name, "skill name"); Name(r.Category, "category");
        if (!new[] { "Beginner", "Intermediate", "Advanced", "Expert" }.Contains(r.Proficiency)) throw new InvalidOperationException("Choose a listed skill proficiency.");
        if ((r.Source?.Length ?? 0) > 500) throw new InvalidOperationException("Skill source must be at most 500 characters.");
    }
    public Task<SkillDto?> SaveSkillAsync(Guid userId, Guid? id, CreateSkillRequest r, CancellationToken ct)
    {
        ValidateSkill(r); return WriteAsync<SkillDto?>(userId, async () => {
            var skill = id.HasValue ? await db.Skills.FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId, ct) : new Skill { UserId = userId };
            if (skill == null) return null;
            var normalized = r.Name.Trim().ToUpperInvariant(); var existing = (await db.Skills.Where(s => s.UserId == userId).ToListAsync(ct)).FirstOrDefault(s => s.Id != skill.Id && s.Name.Trim().ToUpperInvariant() == normalized);
            if (existing != null) { if (!id.HasValue) return Map(existing); throw new InvalidOperationException("A skill with that name already exists. Edit the existing skill instead."); }
            skill.Name = r.Name.Trim(); skill.Category = r.Category.Trim(); skill.Proficiency = r.Proficiency; skill.Source = Text(r.Source);
            if (!id.HasValue) db.Skills.Add(skill); return Map(skill);
        }, ct);
    }
    public async Task SaveExtractedAsync(Guid userId, List<ExtractedSkillDto> skills, CancellationToken ct)
    {
        if (skills == null || skills.Count > 100) throw new InvalidOperationException("Review and save at most 100 skills at a time.");
        foreach (var s in skills) { if (s == null) throw new InvalidOperationException("Remove empty skill rows."); ValidateSkill(new() { Name = s.Name, Category = s.Category, Proficiency = s.Proficiency }); }
        await WriteAsync(userId, async () => {
            var names = (await db.Skills.Where(s => s.UserId == userId).Select(s => s.Name).ToListAsync(ct)).Select(n => n.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var s in skills) if (names.Add(s.Name.Trim())) db.Skills.Add(new() { UserId = userId, Name = s.Name.Trim(), Category = s.Category.Trim(), Proficiency = s.Proficiency, Source = "AI Extracted" });
            return true;
        }, ct);
    }
    public async Task<List<CertificateDto>> CertificatesAsync(Guid userId, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime((await days.TodayAsync(userId, ct)).Date); var records = await db.Certificates.AsNoTracking().Where(c => c.UserId == userId).OrderByDescending(c => c.DateObtained).ToListAsync(ct);
        return records.Select(c => Map(c, today)).ToList();
    }
    public async Task<CertificateDto?> SaveCertificateAsync(Guid userId, Guid? id, CreateCertificateRequest r, CancellationToken ct)
    {
        var name = Name(r.Name, "certificate name"); Dates(r.DateObtained, r.Expiry); var url = Url(r.Url); var today = DateOnly.FromDateTime((await days.TodayAsync(userId, ct)).Date);
        return await WriteAsync<CertificateDto?>(userId, async () => {
            var cert = id.HasValue ? await db.Certificates.FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId, ct) : new Certificate { UserId = userId }; if (cert == null) return null;
            cert.Name = name; cert.Issuer = Text(r.Issuer); cert.DateObtained = Text(r.DateObtained); cert.Expiry = Text(r.Expiry); cert.CredentialId = Text(r.CredentialId); cert.Url = url; cert.Description = Text(r.Description);
            if (!id.HasValue) db.Certificates.Add(cert); return Map(cert, today);
        }, ct);
    }
    public Task<WorkExperienceDto?> SaveWorkAsync(Guid userId, Guid? id, CreateWorkExperienceRequest r, CancellationToken ct)
    {
        var company = Name(r.Company, "company name"); var title = Name(r.Title, "job title"); Dates(r.StartDate, r.IsCurrent ? null : r.EndDate);
        return WriteAsync<WorkExperienceDto?>(userId, async () => {
            var work = id.HasValue ? await db.WorkExperiences.FirstOrDefaultAsync(w => w.Id == id && w.UserId == userId, ct) : new WorkExperience { UserId = userId }; if (work == null) return null;
            work.Company = company; work.Title = title; work.Location = Text(r.Location); work.StartDate = Text(r.StartDate); work.EndDate = r.IsCurrent ? null : Text(r.EndDate); work.IsCurrent = r.IsCurrent; work.Bullets = Text(r.Bullets);
            if (!id.HasValue) db.WorkExperiences.Add(work); return new() { Id = work.Id, Company = work.Company, Title = work.Title, Location = work.Location, StartDate = work.StartDate, EndDate = work.EndDate, IsCurrent = work.IsCurrent, Bullets = work.Bullets };
        }, ct);
    }
    private static void Apply(Project p, CreateProjectRequest r)
    {
        p.Name = Name(r.Name, "project name"); Dates(r.StartDate, r.IsCurrent ? null : r.EndDate); p.Link = Url(r.Link);
        p.Description = Text(r.Description); p.Technologies = Text(r.Technologies); p.StartDate = Text(r.StartDate); p.EndDate = r.IsCurrent ? null : Text(r.EndDate); p.IsCurrent = r.IsCurrent; p.IsPortfolioProject = r.IsPortfolioProject;
        if (!p.IsPortfolioProject) p.IsFeatured = false;
    }
    public Task<ProjectDto?> SaveProjectAsync(Guid userId, Guid? id, CreateProjectRequest r, CancellationToken ct) => WriteAsync<ProjectDto?>(userId, async () => {
        var p = id.HasValue ? await db.Projects.FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId, ct) : new Project { UserId = userId }; if (p == null) return null;
        Apply(p, r); if (!id.HasValue) db.Projects.Add(p); return new() { Id = p.Id, Name = p.Name, Description = p.Description, Technologies = p.Technologies, Link = p.Link, StartDate = p.StartDate, EndDate = p.EndDate, IsCurrent = p.IsCurrent, IsPortfolioProject = p.IsPortfolioProject };
    }, ct);
    private static void ApplyPortfolio(Project p, string? repo, string? deployed, string? screenshot, bool featured, bool portfolio)
    {
        var normalized = Text(repo) == null ? null : GitHub(repo); var deployedUrl = Url(deployed); var screenshotUrl = Url(screenshot);
        if (!string.Equals(p.GitHubRepoUrl, normalized, StringComparison.OrdinalIgnoreCase)) p.GitHubStars = null;
        p.GitHubRepoUrl = normalized; p.DeployedUrl = deployedUrl; p.ScreenshotUrl = screenshotUrl; p.IsPortfolioProject = portfolio; p.IsFeatured = portfolio && featured;
    }
    public Task<PortfolioProjectDto?> SavePortfolioAsync(Guid userId, Guid id, SavePortfolioProjectRequest r, CancellationToken ct) => WriteAsync<PortfolioProjectDto?>(userId, async () => {
        var p = await db.Projects.FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId, ct); if (p == null) return null;
        Apply(p, r); ApplyPortfolio(p, r.GitHubRepoUrl, r.DeployedUrl, r.ScreenshotUrl, r.IsFeatured, r.IsPortfolioProject); return Map(p);
    }, ct);
    public Task<PortfolioProjectDto?> UpdatePortfolioAsync(Guid userId, Guid id, UpdatePortfolioProjectRequest r, CancellationToken ct) => WriteAsync<PortfolioProjectDto?>(userId, async () => {
        var p = await db.Projects.FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId, ct); if (p == null) return null;
        ApplyPortfolio(p, r.GitHubRepoUrl, r.DeployedUrl, r.ScreenshotUrl, r.IsFeatured, r.IsPortfolioProject); return Map(p);
    }, ct);
    public async Task<PortfolioProjectDto> ImportAsync(Guid userId, string repoUrl, CancellationToken ct)
    {
        var canonical = GitHub(repoUrl); var repo = await github.GetAsync(canonical, ct);
        return await WriteAsync(userId, async () => {
            var owned = await db.Projects.Where(p => p.UserId == userId).ToListAsync(ct);
            var p = owned.FirstOrDefault(p => SameRepo(p.GitHubRepoUrl, canonical) || SameRepo(p.Link, canonical));
            if (p == null) { p = new() { UserId = userId, Name = Name(repo.Name, "project name"), Description = repo.Description, Technologies = repo.Language, Link = canonical }; db.Projects.Add(p); }
            p.IsPortfolioProject = true; p.GitHubRepoUrl = canonical; p.GitHubStars = repo.Stars; return Map(p);
        }, ct);
    }
    private static bool SameRepo(string? url, string canonical) { try { return GitHub(url).Equals(canonical, StringComparison.OrdinalIgnoreCase); } catch (InvalidOperationException) { return false; } }
    public async Task<PortfolioProjectDto?> SyncAsync(Guid userId, Guid id, CancellationToken ct)
    {
        var existing = await db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId, ct); if (existing == null) return null;
        var canonical = GitHub(existing.GitHubRepoUrl); var repo = await github.GetAsync(canonical, ct);
        return await WriteAsync<PortfolioProjectDto?>(userId, async () => {
            var p = await db.Projects.FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId, ct); if (p == null) return null;
            if (!SameRepo(p.GitHubRepoUrl, canonical)) throw new InvalidOperationException("The repository link changed during sync. Refresh and try again.");
            p.GitHubStars = repo.Stars; if (string.IsNullOrWhiteSpace(p.Description)) p.Description = repo.Description; if (string.IsNullOrWhiteSpace(p.Technologies)) p.Technologies = repo.Language; return Map(p);
        }, ct);
    }
    private static SkillDto Map(Skill s) => new() { Id = s.Id, Name = s.Name, Category = s.Category, Proficiency = s.Proficiency, Source = s.Source };
    private static CertificateDto Map(Certificate c, DateOnly today) => new() { Id = c.Id, Name = c.Name, Issuer = c.Issuer, DateObtained = c.DateObtained, Expiry = c.Expiry, CredentialId = c.CredentialId, Url = c.Url, Description = c.Description, ExpiryStatus = Expiry(c.Expiry, today) };
    public static PortfolioProjectDto Map(Project p) => new() { Id = p.Id, Name = p.Name, Description = p.Description, Technologies = p.Technologies, Link = p.Link, StartDate = p.StartDate, EndDate = p.EndDate, IsCurrent = p.IsCurrent, GitHubRepoUrl = p.GitHubRepoUrl, DeployedUrl = p.DeployedUrl, ScreenshotUrl = p.ScreenshotUrl, IsFeatured = p.IsFeatured, GitHubStars = p.GitHubStars, ReadmePreview = p.ReadmePreview };
}
