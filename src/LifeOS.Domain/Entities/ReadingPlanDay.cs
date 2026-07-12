namespace LifeOS.Domain.Entities;

public class ReadingPlanDay : BaseEntity
{
    public Guid PlanId { get; set; }
    public ReadingPlan Plan { get; set; } = null!;

    public int DayNumber { get; set; }
    public Guid BookId { get; set; }
    public BibleBook Book { get; set; } = null!;
    public int Chapter { get; set; }
    public bool IsCompleted { get; set; } = false;
    public DateTimeOffset? CompletedAt { get; set; }
}
