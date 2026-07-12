namespace LifeOS.Domain.Entities;

public class ProblemAttempt : BaseEntity
{
    public Guid ProblemId { get; set; }
    public CodingProblem Problem { get; set; } = null!;

    public DateTimeOffset SolvedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? SolutionLanguage { get; set; }
    public int? TimeSpentMinutes { get; set; }
    public string? Notes { get; set; }
    public int XpEarned { get; set; }
}
