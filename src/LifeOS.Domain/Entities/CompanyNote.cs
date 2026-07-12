namespace LifeOS.Domain.Entities;

public class CompanyNote : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public string CompanyName { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public int? Rating { get; set; }
}
