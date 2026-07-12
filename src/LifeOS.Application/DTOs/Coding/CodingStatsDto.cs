namespace LifeOS.Application.DTOs.Coding;

public class CodingStatsDto
{
    public int TotalProblems { get; set; }
    public int SolvedProblems { get; set; }
    public int EasySolved { get; set; }
    public int MediumSolved { get; set; }
    public int HardSolved { get; set; }
    public int CurrentStreak { get; set; }
    public int LongestStreak { get; set; }
    public int TotalXpEarned { get; set; }
    public List<CategoryStatDto> ByCategory { get; set; } = new();
    public List<LanguageStatDto> ByLanguage { get; set; } = new();
}

public class CategoryStatDto
{
    public string Category { get; set; } = string.Empty;
    public int SolvedCount { get; set; }
}

public class LanguageStatDto
{
    public string Language { get; set; } = string.Empty;
    public int SolvedCount { get; set; }
}
