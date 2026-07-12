namespace LifeOS.Application.DTOs.Coding;

public class CodingProblemDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Platform { get; set; }
    public string? Url { get; set; }
    public string Difficulty { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string? Description { get; set; }
    public string? Notes { get; set; }
    public bool IsSolved { get; set; }
    public DateTimeOffset? SolvedAt { get; set; }
    public string? SolutionLanguage { get; set; }
    public int? TimeSpentMinutes { get; set; }
    public int AttemptCount { get; set; }
}

public class CreateCodingProblemRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Platform { get; set; }
    public string? Url { get; set; }
    public string Difficulty { get; set; } = "easy";
    public string? Category { get; set; }
    public string? Description { get; set; }
    public string? Notes { get; set; }
}

public class UpdateCodingProblemRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Platform { get; set; }
    public string? Url { get; set; }
    public string Difficulty { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string? Description { get; set; }
    public string? Notes { get; set; }
}

public class SolveProblemRequest
{
    public string? SolutionLanguage { get; set; }
    public int? TimeSpentMinutes { get; set; }
    public string? Notes { get; set; }
}

public class ProblemAttemptDto
{
    public Guid Id { get; set; }
    public Guid ProblemId { get; set; }
    public DateTimeOffset SolvedAt { get; set; }
    public string? SolutionLanguage { get; set; }
    public int? TimeSpentMinutes { get; set; }
    public string? Notes { get; set; }
    public int XpEarned { get; set; }
}
