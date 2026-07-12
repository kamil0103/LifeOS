namespace LifeOS.Application.DTOs.Portfolio;

public class PortfolioProjectDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Technologies { get; set; }
    public string? Link { get; set; }
    public string? StartDate { get; set; }
    public string? EndDate { get; set; }
    public bool IsCurrent { get; set; }
    public string? GitHubRepoUrl { get; set; }
    public string? DeployedUrl { get; set; }
    public string? ScreenshotUrl { get; set; }
    public bool IsFeatured { get; set; }
    public int? GitHubStars { get; set; }
    public string? ReadmePreview { get; set; }
}

public class UpdatePortfolioProjectRequest
{
    public string? GitHubRepoUrl { get; set; }
    public string? DeployedUrl { get; set; }
    public string? ScreenshotUrl { get; set; }
    public bool IsFeatured { get; set; }
    public bool IsPortfolioProject { get; set; }
}

public class GitHubRepoInfoDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Stars { get; set; }
    public string? Language { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string? HtmlUrl { get; set; }
}
