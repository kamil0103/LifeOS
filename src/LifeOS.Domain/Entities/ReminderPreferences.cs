namespace LifeOS.Domain.Entities;
public class ReminderPreferences
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public bool FollowUpsEnabled { get; set; } = true;
    public bool CalendarEnabled { get; set; } = true;
    public bool CodingReviewsEnabled { get; set; } = true;
    public int CalendarLeadMinutes { get; set; } = 30;
}
