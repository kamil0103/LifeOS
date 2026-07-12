using System.Text.Json;
using LifeOS.Application.DTOs.Portfolio;
using LifeOS.Domain.Entities;
using LifeOS.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PortfolioController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IHttpClientFactory _httpClientFactory;

    public PortfolioController(AppDbContext context, IHttpClientFactory httpClientFactory)
    {
        _context = context;
        _httpClientFactory = httpClientFactory;
    }

    private Guid GetUserId()
    {
        var sub = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        return Guid.Parse(sub!);
    }

    [HttpGet]
    public async Task<ActionResult<List<PortfolioProjectDto>>> GetPortfolio(CancellationToken ct)
    {
        var userId = GetUserId();
        var projects = await _context.Projects
            .AsNoTracking()
            .Where(p => p.UserId == userId && p.IsPortfolioProject)
            .OrderByDescending(p => p.IsFeatured)
            .ThenByDescending(p => p.StartDate)
            .ToListAsync(ct);

        return projects.Select(MapToDto).ToList();
    }

    [HttpPut("projects/{id}")]
    public async Task<ActionResult<PortfolioProjectDto>> UpdateProject(Guid id, [FromBody] UpdatePortfolioProjectRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId, ct);
        if (project == null)
            return NotFound();

        project.GitHubRepoUrl = request.GitHubRepoUrl;
        project.DeployedUrl = request.DeployedUrl;
        project.ScreenshotUrl = request.ScreenshotUrl;
        project.IsFeatured = request.IsFeatured;
        project.IsPortfolioProject = request.IsPortfolioProject;

        // Auto-fetch GitHub info if repo URL provided
        if (!string.IsNullOrWhiteSpace(request.GitHubRepoUrl))
        {
            try
            {
                var repoInfo = await FetchGitHubRepoInfo(request.GitHubRepoUrl, ct);
                if (repoInfo != null)
                {
                    project.GitHubStars = repoInfo.Stars;
                    if (string.IsNullOrWhiteSpace(project.Description))
                        project.Description = repoInfo.Description;
                }
            }
            catch { /* GitHub fetch is best effort */ }
        }

        await _context.SaveChangesAsync(ct);
        return MapToDto(project);
    }

    [HttpPost("projects/{id}/sync-github")]
    public async Task<ActionResult<PortfolioProjectDto>> SyncGitHub(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId, ct);
        if (project == null)
            return NotFound();

        if (string.IsNullOrWhiteSpace(project.GitHubRepoUrl))
            return BadRequest(new ProblemDetails { Title = "No GitHub URL", Detail = "Set a GitHub repo URL first." });

        var repoInfo = await FetchGitHubRepoInfo(project.GitHubRepoUrl, ct);
        if (repoInfo == null)
            return BadRequest(new ProblemDetails { Title = "GitHub fetch failed", Detail = "Could not fetch repo info from GitHub." });

        project.GitHubStars = repoInfo.Stars;
        if (!string.IsNullOrWhiteSpace(repoInfo.Description))
            project.Description = repoInfo.Description;
        if (!string.IsNullOrWhiteSpace(repoInfo.Language) && string.IsNullOrWhiteSpace(project.Technologies))
            project.Technologies = repoInfo.Language;

        await _context.SaveChangesAsync(ct);
        return MapToDto(project);
    }

    [HttpGet("github/{username}/repos")]
    public async Task<ActionResult<List<GitHubRepoInfoDto>>> GetGitHubRepos(string username, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri("https://api.github.com/");
        client.DefaultRequestHeaders.Add("User-Agent", "LifeOS-Portfolio-Importer");

        try
        {
            var response = await client.GetAsync($"users/{username}/repos?sort=updated&per_page=30", ct);
            if (!response.IsSuccessStatusCode)
                return BadRequest(new ProblemDetails { Title = "GitHub error", Detail = "Could not fetch repos." });

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            var repos = new List<GitHubRepoInfoDto>();

            foreach (var element in doc.RootElement.EnumerateArray())
            {
                repos.Add(new GitHubRepoInfoDto
                {
                    Name = element.GetProperty("name").GetString() ?? "",
                    Description = element.TryGetProperty("description", out var desc) ? desc.GetString() : null,
                    Stars = element.TryGetProperty("stargazers_count", out var stars) ? stars.GetInt32() : 0,
                    Language = element.TryGetProperty("language", out var lang) ? lang.GetString() : null,
                    UpdatedAt = element.TryGetProperty("updated_at", out var updated) && updated.TryGetDateTimeOffset(out var dt) ? dt : DateTimeOffset.UtcNow,
                    HtmlUrl = element.TryGetProperty("html_url", out var url) ? url.GetString() : null
                });
            }

            return repos.OrderByDescending(r => r.UpdatedAt).ToList();
        }
        catch (Exception ex)
        {
            return BadRequest(new ProblemDetails { Title = "GitHub fetch failed", Detail = ex.Message });
        }
    }

    private async Task<GitHubRepoInfoDto?> FetchGitHubRepoInfo(string repoUrl, CancellationToken ct)
    {
        // Extract owner/repo from URL like https://github.com/owner/repo
        var parts = repoUrl.Replace("https://github.com/", "").Trim('/').Split('/');
        if (parts.Length < 2) return null;

        var owner = parts[0];
        var repo = parts[1].Split('/')[0]; // Remove any trailing paths

        var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri("https://api.github.com/");
        client.DefaultRequestHeaders.Add("User-Agent", "LifeOS-Portfolio-Importer");

        try
        {
            var response = await client.GetAsync($"repos/{owner}/{repo}", ct);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);

            return new GitHubRepoInfoDto
            {
                Name = doc.RootElement.GetProperty("name").GetString() ?? "",
                Description = doc.RootElement.TryGetProperty("description", out var desc) ? desc.GetString() : null,
                Stars = doc.RootElement.TryGetProperty("stargazers_count", out var stars) ? stars.GetInt32() : 0,
                Language = doc.RootElement.TryGetProperty("language", out var lang) ? lang.GetString() : null,
                UpdatedAt = doc.RootElement.TryGetProperty("updated_at", out var updated) && updated.TryGetDateTimeOffset(out var dt) ? dt : DateTimeOffset.UtcNow,
                HtmlUrl = doc.RootElement.TryGetProperty("html_url", out var url) ? url.GetString() : null
            };
        }
        catch
        {
            return null;
        }
    }

    private static PortfolioProjectDto MapToDto(Project p)
    {
        return new PortfolioProjectDto
        {
            Id = p.Id,
            Name = p.Name,
            Description = p.Description,
            Technologies = p.Technologies,
            Link = p.Link,
            StartDate = p.StartDate,
            EndDate = p.EndDate,
            IsCurrent = p.IsCurrent,
            GitHubRepoUrl = p.GitHubRepoUrl,
            DeployedUrl = p.DeployedUrl,
            ScreenshotUrl = p.ScreenshotUrl,
            IsFeatured = p.IsFeatured,
            GitHubStars = p.GitHubStars,
            ReadmePreview = p.ReadmePreview
        };
    }
}
