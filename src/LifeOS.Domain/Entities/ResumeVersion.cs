namespace LifeOS.Domain.Entities;

public class ResumeVersion : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public string Title { get; set; } = string.Empty;
    public string Template { get; set; } = "modern";
    public string ContentJson { get; set; } = "{}";
}
