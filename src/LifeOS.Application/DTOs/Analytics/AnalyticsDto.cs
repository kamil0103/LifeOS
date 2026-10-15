namespace LifeOS.Application.DTOs.Analytics;
public record XpSourceDto(string Source, int Count, int Xp);
public record XpDayDto(string Date, int Xp, List<XpSourceDto> Source);
public record HabitDayDto(string Date, int Count, List<string> Habits);
public record JobFunnelDto(int Saved, int Applied, int Phone_screen, int Interview, int Offer, int Rejected, int Withdrawn, int Total);
public record CodingStreakDto(int Current, int Longest, int TotalDays, int TotalProblems, int TotalAttempts);
public record OverviewDto(int TotalXp, int HabitsCount, int JobsCount, int CodingCount, int DocsCount, int CodingAttempts);
public record AnalyticsReportDto(string StartDate, string EndDate, string TimeZone, int Days, List<XpDayDto> XpTrends, List<HabitDayDto> HabitHeatmap, JobFunnelDto JobFunnel, CodingStreakDto CodingStreaks, OverviewDto Overview);
