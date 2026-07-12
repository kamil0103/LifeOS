namespace LifeOS.Application.DTOs.Skills;

public class SkillDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Proficiency { get; set; } = string.Empty;
    public string? Source { get; set; }
}

public class CreateSkillRequest
{
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = "Other";
    public string Proficiency { get; set; } = "Beginner";
    public string? Source { get; set; }
}

public class UpdateSkillRequest
{
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = "Other";
    public string Proficiency { get; set; } = "Beginner";
    public string? Source { get; set; }
}
