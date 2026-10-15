using LifeOS.Application.DTOs.Analytics;
namespace LifeOS.Application.Interfaces;
public interface IAnalyticsService
{
    Task<List<XpDayDto>> XpAsync(Guid userId, int days, CancellationToken ct);
    Task<List<HabitDayDto>> HabitsAsync(Guid userId, int days, CancellationToken ct);
    Task<JobFunnelDto> FunnelAsync(Guid userId, CancellationToken ct);
    Task<CodingStreakDto> CodingAsync(Guid userId, CancellationToken ct);
    Task<OverviewDto> OverviewAsync(Guid userId, CancellationToken ct);
    Task<AnalyticsReportDto> ReportAsync(Guid userId, int days, CancellationToken ct);
}
