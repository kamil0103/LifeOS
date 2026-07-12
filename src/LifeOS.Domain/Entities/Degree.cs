namespace LifeOS.Domain.Entities;

public class Degree : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public Guid? InstitutionId { get; set; }
    public Institution? Institution { get; set; }

    public string DegreeName { get; set; } = string.Empty;
    public string DegreeType { get; set; } = "other";
    public string? Field { get; set; }
    public string? StartDate { get; set; }
    public string? EndDate { get; set; }
    public string? Gpa { get; set; }
    public string? Honors { get; set; }
    public bool IsCurrent { get; set; } = false;

    public List<Course> Courses { get; set; } = [];
}
