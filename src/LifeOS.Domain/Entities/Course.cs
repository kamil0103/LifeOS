namespace LifeOS.Domain.Entities;

public class Course : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public Guid? DegreeId { get; set; }
    public Degree? Degree { get; set; }

    public Guid? InstitutionId { get; set; }
    public Institution? Institution { get; set; }

    public string? Code { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Grade { get; set; }
    public decimal? Credits { get; set; }
    public string? Term { get; set; }
    public string? Description { get; set; }
    public bool IsMajorRelated { get; set; } = true;
}
