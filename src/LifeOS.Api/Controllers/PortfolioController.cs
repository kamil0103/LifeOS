using LifeOS.Application.DTOs.Portfolio;
using LifeOS.Application.Interfaces;
using LifeOS.Infrastructure.Data;
using LifeOS.Infrastructure.Professional;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace LifeOS.Api.Controllers;
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PortfolioController(AppDbContext db, IProfessionalRecordService records, IGitHubRepositoryClient github) : ControllerBase
{
    private Guid UserId => Guid.Parse((User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier) ?? User.FindFirst("sub"))!.Value);
    [HttpGet]
    public async Task<ActionResult<List<PortfolioProjectDto>>> GetPortfolio(CancellationToken ct)
        => (await db.Projects.AsNoTracking().Where(p => p.UserId == UserId && p.IsPortfolioProject).OrderByDescending(p => p.IsFeatured).ThenByDescending(p => p.StartDate).ToListAsync(ct)).Select(ProfessionalRecordService.Map).ToList();
    [HttpPut("projects/{id:guid}")]
    public async Task<ActionResult<PortfolioProjectDto>> UpdateProject(Guid id, UpdatePortfolioProjectRequest request, CancellationToken ct)
        => await records.UpdatePortfolioAsync(UserId, id, request, ct) is { } result ? Ok(result) : NotFound();
    [HttpPut("projects/{id:guid}/details")]
    public async Task<ActionResult<PortfolioProjectDto>> SaveProject(Guid id, SavePortfolioProjectRequest request, CancellationToken ct)
        => await records.SavePortfolioAsync(UserId, id, request, ct) is { } result ? Ok(result) : NotFound();
    [HttpPost("import")]
    public async Task<ActionResult<PortfolioProjectDto>> Import(ImportGitHubProjectRequest request, CancellationToken ct)
        => await records.ImportAsync(UserId, request.RepoUrl, ct);
    [HttpPost("projects/{id:guid}/sync-github")]
    public async Task<ActionResult<PortfolioProjectDto>> SyncGitHub(Guid id, CancellationToken ct)
        => await records.SyncAsync(UserId, id, ct) is { } result ? Ok(result) : NotFound();
    [HttpGet("github/{username}/repos")]
    public async Task<ActionResult<List<GitHubRepoInfoDto>>> GetGitHubRepos(string username, CancellationToken ct)
        => await github.ListAsync(username, ct);
}
