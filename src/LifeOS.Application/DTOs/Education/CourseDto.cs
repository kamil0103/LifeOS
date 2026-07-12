namespace LifeOS.Application.DTOs.Education;

public class CourseDto
{
    public Guid Id { get; set; }
    public Guid? DegreeId { get; set; }
    public string? Code { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Grade { get; set; }
    public decimal? Credits { get; set; }
    public string? Term { get; set; }
    public string? Description { get; set; }
    public bool IsMajorRelated { get; set; } = true;
}

public class CreateCourseRequest
{
    public Guid? DegreeId { get; set; }
    public Guid? InstitutionId { get; set; }
    public string? Code { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Grade { get; set; }
    public decimal? Credits { get; set; }
    public string? Term { get; set; }
    public string? Description { get; set; }
    public bool IsMajorRelated { get; set; } = true;
}

public class UpdateCourseRequest
{
    public Guid? DegreeId { get; set; }
    public Guid? InstitutionId { get; set; }
    public string? Code { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Grade { get; set; }
    public decimal? Credits { get; set; }
    public string? Term { get; set; }
    public string? Description { get; set; }
    public bool IsMajorRelated { get; set; } = true;
}
