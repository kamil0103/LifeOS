namespace LifeOS.Application.DTOs.Jobs;

public class JobDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public string? Location { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? Url { get; set; }
    public string Source { get; set; } = "manual";
    public string? SalaryRange { get; set; }
    public string? JobType { get; set; }
    public DateTime? PostedDate { get; set; }
    public int? MatchScore { get; set; }
    public string Status { get; set; } = "saved";
    public DateTimeOffset CreatedAt { get; set; }
}

public class CreateJobRequest
{
    public string Title { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public string? Location { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? Url { get; set; }
    public string Source { get; set; } = "manual";
    public string? SalaryRange { get; set; }
    public string? JobType { get; set; }
    public DateTime? PostedDate { get; set; }
}

public class UpdateJobRequest
{
    public string Title { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public string? Location { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? Url { get; set; }
    public string? SalaryRange { get; set; }
    public string? JobType { get; set; }
    public DateTime? PostedDate { get; set; }
    public int? MatchScore { get; set; }
    public string Status { get; set; } = "saved";
}

public class AnalyzeJobRequest
{
    public string JobDescription { get; set; } = string.Empty;
}

public class JobMatchResult
{
    public int MatchScore { get; set; }
    public string Analysis { get; set; } = string.Empty;
    public List<string> MatchedSkills { get; set; } = [];
    public List<string> MissingSkills { get; set; } = [];
}

public class ExternalJobDto
{
    public string Title { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public string? Location { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? Url { get; set; }
    public string Source { get; set; } = string.Empty;
    public string? SalaryRange { get; set; }
    public string? JobType { get; set; }
    public DateTime? PostedDate { get; set; }
}
