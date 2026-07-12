namespace LifeOS.Domain.Entities;

public class AiCoachMessage : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public string MessageType { get; set; } = string.Empty; // mission, warning, encouragement, suggestion
    public string Content { get; set; } = string.Empty;
    public string? ContextJson { get; set; }
    public bool IsRead { get; set; } = false;
}
