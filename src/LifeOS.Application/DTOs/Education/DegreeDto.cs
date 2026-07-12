namespace LifeOS.Application.DTOs.Education;

public class DegreeDto
{
    public Guid Id { get; set; }
    public Guid? InstitutionId { get; set; }
    public string? InstitutionName { get; set; }
    public string DegreeName { get; set; } = string.Empty;
    public string DegreeType { get; set; } = string.Empty;
    public string? Field { get; set; }
    public string? StartDate { get; set; }
    public string? EndDate { get; set; }
    public string? Gpa { get; set; }
    public string? Honors { get; set; }
    public bool IsCurrent { get; set; }
    public List<CourseDto> Courses { get; set; } = [];
}

public class CreateDegreeRequest
{
    public Guid? InstitutionId { get; set; }
    public string DegreeName { get; set; } = string.Empty;
    public string DegreeType { get; set; } = "other";
    public string? Field { get; set; }
    public string? StartDate { get; set; }
    public string? EndDate { get; set; }
    public string? Gpa { get; set; }
    public string? Honors { get; set; }
    public bool IsCurrent { get; set; } = false;
}

public class UpdateDegreeRequest
{
    public Guid? InstitutionId { get; set; }
    public string DegreeName { get; set; } = string.Empty;
    public string DegreeType { get; set; } = "other";
    public string? Field { get; set; }
    public string? StartDate { get; set; }
    public string? EndDate { get; set; }
    public string? Gpa { get; set; }
    public string? Honors { get; set; }
    public bool IsCurrent { get; set; } = false;
}
