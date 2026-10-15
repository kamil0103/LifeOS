namespace LifeOS.Application.DTOs.Coach;
public class DailyMissionDto
{
    public Guid Id { get; set; }
    public DateTimeOffset Date { get; set; }
    public List<MissionPriority> Priorities { get; set; } = [];
    public string? AiSummary { get; set; }
    public bool IsCompleted { get; set; }
}

public class MissionPriority
{
    public bool Completed { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}

public class AiMissionResponse
{
    public List<MissionPriority> Priorities { get; set; } = [];
    public string Summary { get; set; } = string.Empty;
    public string? Warning { get; set; }
    public string? Encouragement { get; set; }
}

public class AiCoachMessageDto
{
    public Guid Id { get; set; }
    public string MessageType { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}


public record CoachPreferences(bool IncludeProfile=true,bool IncludeProgress=true,bool IncludeCalendar=true,bool IncludeJournal=false);
public record CoachSection(string Source,string Content);
public record CoachContext(string Date,string TimeZone,DateTimeOffset BuiltAt,List<CoachSection> Sections,string? Warning);
public record CoachHistoryItem(Guid Id,string Role,string Content,DateTimeOffset CreatedAt);
public record CoachMemoryDto(Guid Id,string Content,DateTimeOffset UpdatedAt);
public record CoachChatRequest(string Message,Guid? RequestId=null);
public record CoachChatReply(string Response,Guid RequestId,List<string> ContextSources,bool WasReplay=false);
public record CoachMemoryRequest(string Content);
