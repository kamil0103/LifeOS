namespace LifeOS.Application.DTOs.JobMatch;

public class JobMatchRequest
{
    public Guid JobId { get; set; }
}

public class JobMatchResultDto
{
    public int MatchScore { get; set; } // 0-100
    public List<string> MatchedSkills { get; set; } = new();
    public List<string> MissingSkills { get; set; } = new();
    public List<string> SuggestedImprovements { get; set; } = new();
    public string Summary { get; set; } = string.Empty;
}

public class InterviewQaRequest
{
    public Guid JobId { get; set; }
}

public class InterviewQaDto
{
    public List<InterviewQuestionDto> Questions { get; set; } = new();
    public string RoleFocus { get; set; } = string.Empty;
    public string PreparationTips { get; set; } = string.Empty;
}

public class InterviewQuestionDto
{
    public string Question { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty; // technical, behavioral, system_design
    public string? SuggestedAnswer { get; set; }
    public string? KeyPoints { get; set; }
}
