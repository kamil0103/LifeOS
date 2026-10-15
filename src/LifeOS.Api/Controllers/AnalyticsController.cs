using LifeOS.Application.DTOs.Analytics;
using LifeOS.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace LifeOS.Api.Controllers;
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AnalyticsController(IAnalyticsService analytics) : ControllerBase
{
    private Guid UserId => Guid.Parse((User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier) ?? User.FindFirst("sub"))!.Value);
    [HttpGet("report")]
    public async Task<ActionResult<AnalyticsReportDto>> Report(int days = 30, CancellationToken ct = default) => await analytics.ReportAsync(UserId, days, ct);
    [HttpGet("xp-trends")]
    public async Task<ActionResult<List<XpDayDto>>> Xp(int days = 30, CancellationToken ct = default) => await analytics.XpAsync(UserId, days, ct);
    [HttpGet("habit-heatmap")]
    public async Task<ActionResult<List<HabitDayDto>>> Habits(int days = 90, CancellationToken ct = default) => await analytics.HabitsAsync(UserId, days, ct);
    [HttpGet("job-funnel")]
    public async Task<ActionResult<JobFunnelDto>> Funnel(CancellationToken ct) => await analytics.FunnelAsync(UserId, ct);
    [HttpGet("coding-streaks")]
    public async Task<ActionResult<CodingStreakDto>> Coding(CancellationToken ct) => await analytics.CodingAsync(UserId, ct);
    [HttpGet("overview")]
    public async Task<ActionResult<OverviewDto>> Overview(CancellationToken ct) => await analytics.OverviewAsync(UserId, ct);
}
