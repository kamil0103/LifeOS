using LifeOS.Application.DTOs.Analytics;
using LifeOS.Application.Interfaces;
using LifeOS.Application.Services;
using LifeOS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
namespace LifeOS.Infrastructure.Personal;
public class AnalyticsService(AppDbContext db, ILocalDayService days) : IAnalyticsService
{
    private async Task<List<XpDayDto>> XpAsync(Guid userId, int count, LocalDay day, CancellationToken ct)
    {
        count = Math.Clamp(count, 1, 365); var start = day.Offset(1 - count).Start;
        var rows = await db.XpTransactions.AsNoTracking().Where(x => x.UserId == userId && x.CreatedAt >= start && x.CreatedAt < day.End).ToListAsync(ct);
        var groups = rows.GroupBy(x => day.DateOf(x.CreatedAt)).ToDictionary(g => g.Key, g => g.ToList());
        return Enumerable.Range(1 - count, count).Select(offset => { var date = day.Date.AddDays(offset); var items = groups.GetValueOrDefault(date) ?? [];
            return new XpDayDto(date.ToString("yyyy-MM-dd"), items.Sum(x => x.Amount), items.GroupBy(x => x.Source).OrderBy(g => g.Key).Select(g => new XpSourceDto(g.Key, g.Count(), g.Sum(x => x.Amount))).ToList()); }).ToList();
    }
    private async Task<List<HabitDayDto>> HabitsAsync(Guid userId, int count, LocalDay day, CancellationToken ct)
    {
        count = Math.Clamp(count, 1, 365); var start = day.Offset(1 - count).Start;
        var rows = await db.HabitCompletions.AsNoTracking().Where(x => x.Habit.UserId == userId && x.IsCompleted && x.CompletedAt >= start && x.CompletedAt < day.End).Select(x => new { x.HabitId, x.Habit.Name, x.CompletedAt }).ToListAsync(ct);
        var groups = rows.GroupBy(x => day.DateOf(x.CompletedAt)).ToDictionary(g => g.Key, g => g.ToList());
        return Enumerable.Range(1 - count, count).Select(offset => { var date = day.Date.AddDays(offset); var items = groups.GetValueOrDefault(date) ?? [];
            return new HabitDayDto(date.ToString("yyyy-MM-dd"), items.Select(x => x.HabitId).Distinct().Count(), items.Select(x => x.Name).Distinct().OrderBy(n => n).ToList()); }).ToList();
    }
    public async Task<List<XpDayDto>> XpAsync(Guid userId, int count, CancellationToken ct) => await XpAsync(userId, count, await days.TodayAsync(userId, ct), ct);
    public async Task<List<HabitDayDto>> HabitsAsync(Guid userId, int count, CancellationToken ct) => await HabitsAsync(userId, count, await days.TodayAsync(userId, ct), ct);
    public async Task<JobFunnelDto> FunnelAsync(Guid userId, CancellationToken ct)
    {
        var statuses = await db.JobApplications.AsNoTracking().Where(a => a.UserId == userId).Select(a => a.Status).ToListAsync(ct);
        var saved = await db.Jobs.CountAsync(j => j.UserId == userId && !j.Applications.Any(a => a.UserId == userId), ct);
        return new(saved, statuses.Count(s => s == "applied"), statuses.Count(s => s == "phone_screen"), statuses.Count(s => s == "interview"), statuses.Count(s => s == "offer"), statuses.Count(s => s == "rejected"), statuses.Count(s => s == "withdrawn"), statuses.Count);
    }
    private async Task<CodingStreakDto> CodingAsync(Guid userId, LocalDay day, CancellationToken ct)
    {
        var rows = await db.ProblemAttempts.AsNoTracking().Where(a => a.Problem.UserId == userId && a.SolvedAt < day.End).Select(a => new { a.ProblemId, a.SolvedAt }).ToListAsync(ct);
        var dates = rows.Select(a => day.DateOf(a.SolvedAt)).Distinct().OrderBy(d => d).ToList(); var longest = 0; var run = 0; DateTime? previous = null;
        foreach (var date in dates) { run = previous.HasValue && date == previous.Value.AddDays(1) ? run + 1 : 1; longest = Math.Max(longest, run); previous = date; }
        var current = 0; var expected = dates.LastOrDefault() == day.Date ? day.Date : day.Date.AddDays(-1);
        foreach (var date in dates.AsEnumerable().Reverse()) { if (date != expected) break; current++; expected = expected.AddDays(-1); }
        return new(current, longest, dates.Count, rows.Select(a => a.ProblemId).Distinct().Count(), rows.Count);
    }
    public async Task<CodingStreakDto> CodingAsync(Guid userId, CancellationToken ct) => await CodingAsync(userId, await days.TodayAsync(userId, ct), ct);
    public async Task<OverviewDto> OverviewAsync(Guid userId, CancellationToken ct)
    {
        var xp = await db.XpTransactions.Where(x => x.UserId == userId).SumAsync(x => x.Amount, ct);
        var habits = await db.Habits.CountAsync(h => h.UserId == userId && h.IsActive, ct); var jobs = await db.Jobs.CountAsync(j => j.UserId == userId, ct);
        var problems = await db.ProblemAttempts.Where(a => a.Problem.UserId == userId).Select(a => a.ProblemId).Distinct().CountAsync(ct);
        var attempts = await db.ProblemAttempts.CountAsync(a => a.Problem.UserId == userId, ct); var docs = await db.Documents.CountAsync(d => d.UserId == userId, ct);
        return new(xp, habits, jobs, problems, docs, attempts);
    }
    public async Task<AnalyticsReportDto> ReportAsync(Guid userId, int count, CancellationToken ct)
    {
        var day = await days.TodayAsync(userId, ct); count = Math.Clamp(count, 1, 365);
        return new(day.Date.AddDays(1 - count).ToString("yyyy-MM-dd"), day.Date.ToString("yyyy-MM-dd"), day.Zone.Id, count, await XpAsync(userId, count, day, ct), await HabitsAsync(userId, count, day, ct), await FunnelAsync(userId, ct), await CodingAsync(userId, day, ct), await OverviewAsync(userId, ct));
    }
}
