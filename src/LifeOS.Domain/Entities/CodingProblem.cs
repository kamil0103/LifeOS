namespace LifeOS.Domain.Entities;

public class CodingProblem : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public string Title { get; set; } = string.Empty;
    public string? Platform { get; set; } // leetcode, hackerrank, codewars, etc.
    public string? Url { get; set; }
    public string Difficulty { get; set; } = "easy"; // easy, medium, hard
    public string? Category { get; set; } // Arrays, DP, Graphs, etc.
    public string? Description { get; set; }
    public string? Notes { get; set; }
    public bool IsSolved { get; set; }
    public DateTimeOffset? SolvedAt { get; set; }
    public string? SolutionLanguage { get; set; }
    public int? TimeSpentMinutes { get; set; }

    public List<ProblemAttempt> Attempts { get; set; } = [];
}
