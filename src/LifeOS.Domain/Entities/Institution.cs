namespace LifeOS.Domain.Entities;

public class Institution : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public string InstitutionType { get; set; } = "other";
    public string? Location { get; set; }

    public List<Degree> Degrees { get; set; } = [];
    public List<Course> UnassignedCourses { get; set; } = [];
}
