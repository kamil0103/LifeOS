namespace LifeOS.Application.DTOs.Documents;

public class ResumeDataDto
{
    public string Title { get; set; } = "My Resume";
    public string Template { get; set; } = "modern";
    public List<string> SectionOrder { get; set; } = new();

    public ResumeProfileDto Profile { get; set; } = new();
    public List<ResumeExperienceDto> Experience { get; set; } = new();
    public List<ResumeEducationDto> Education { get; set; } = new();
    public List<ResumeSkillGroupDto> Skills { get; set; } = new();
    public List<ResumeProjectDto> Projects { get; set; } = new();
    public List<ResumeCertificationDto> Certifications { get; set; } = new();
    public List<ResumeCourseDto> Courses { get; set; } = new();
}

public class ResumeProfileDto
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string LinkedIn { get; set; } = string.Empty;
    public string Portfolio { get; set; } = string.Empty;
    public string GitHub { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
}

public class ResumeExperienceDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string? StartDate { get; set; }
    public string? EndDate { get; set; }
    public bool IsCurrent { get; set; }
    public string Bullets { get; set; } = string.Empty;
    public List<string> BulletList => Bullets.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(b => b.TrimStart('-', '•', ' ').Trim())
        .Where(b => !string.IsNullOrWhiteSpace(b))
        .ToList();
}

public class ResumeEducationDto
{
    public Guid Id { get; set; }
    public string School { get; set; } = string.Empty;
    public string Degree { get; set; } = string.Empty;
    public string Field { get; set; } = string.Empty;
    public string? GraduationDate { get; set; }
    public string? Gpa { get; set; }
    public string? Honors { get; set; }
    public bool IsCurrent { get; set; }
    public string? StartDate { get; set; }
    public string? EndDate { get; set; }
}

public class ResumeSkillGroupDto
{
    public string Category { get; set; } = string.Empty;
    public List<string> Skills { get; set; } = new();
}

public class ResumeProjectDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Technologies { get; set; } = string.Empty;
    public string Link { get; set; } = string.Empty;
    public string? StartDate { get; set; }
    public string? EndDate { get; set; }
    public bool IsCurrent { get; set; }
}

public class ResumeCertificationDto
{
    public string Name { get; set; } = string.Empty;
    public string Organization { get; set; } = string.Empty;
    public string? Date { get; set; }
}

public class ResumeCourseDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Grade { get; set; }
    public string? Credits { get; set; }
    public string? Term { get; set; }
    public string? Description { get; set; }
}
