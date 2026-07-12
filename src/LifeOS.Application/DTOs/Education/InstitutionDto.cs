using LifeOS.Domain.Entities;

namespace LifeOS.Application.DTOs.Education;

public class InstitutionDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string InstitutionType { get; set; } = string.Empty;
    public string? Location { get; set; }
    public List<DegreeDto> Degrees { get; set; } = [];
    public List<CourseDto> UnassignedCourses { get; set; } = [];
}

public class CreateInstitutionRequest
{
    public string Name { get; set; } = string.Empty;
    public string InstitutionType { get; set; } = "other";
    public string? Location { get; set; }
}

public class UpdateInstitutionRequest
{
    public string Name { get; set; } = string.Empty;
    public string InstitutionType { get; set; } = "other";
    public string? Location { get; set; }
}
