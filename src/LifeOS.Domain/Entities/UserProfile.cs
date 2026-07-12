namespace LifeOS.Domain.Entities;

public class UserProfile
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public string? FullName { get; set; }
    public string? Phone { get; set; }
    public string? Location { get; set; }
    public string? LinkedInUrl { get; set; }
    public string? GitHubUrl { get; set; }
    public string? PortfolioUrl { get; set; }
    public string? Summary { get; set; }
    public string? TargetRoles { get; set; }
    public string? AvatarUrl { get; set; }
}
