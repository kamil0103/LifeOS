using System.Collections.Concurrent;
using LifeOS.Application.DTOs.Coding;
using LifeOS.Application.Interfaces;
using LifeOS.Domain.Entities;
using LifeOS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
namespace LifeOS.Infrastructure.Coding;

public class CodingProgressService(AppDbContext db, ILocalDayService days) : ICodingProgressService
{
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> Gates = new();

    // The row lock also coordinates with reviews completed from the daily plan.
    private async Task<IDbContextTransaction?> LockAsync(Guid userId, Guid id, CancellationToken ct)
    {
        if (!db.Database.IsRelational()) return null;
        var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var key = BitConverter.ToInt64(userId.ToByteArray(), 0) ^ 0x434F44494E47L;
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({key})", ct);
            await db.CodingProblems.Where(p => p.Id == id && p.UserId == userId)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.UpdatedAt, DateTimeOffset.UtcNow), ct);
            return transaction;
        }
        catch { await transaction.DisposeAsync(); throw; }
    }

    public async Task<CodingProblemDto> CreateAsync(Guid userId, CreateCodingProblemRequest request, CancellationToken ct)
    {
        ValidateProblem(request.Title, request.Difficulty, request.Url);
        var problem = new CodingProblem { UserId = userId, Title = request.Title.Trim(), Difficulty = request.Difficulty.ToLowerInvariant(),
            Platform = request.Platform?.Trim(), Url = request.Url?.Trim(), Category = request.Category?.Trim(), Description = request.Description, Notes = request.Notes };
        db.CodingProblems.Add(problem);
        await db.SaveChangesAsync(ct);
        return Map(problem);
    }

    public async Task<CodingProblemDto?> UpdateAsync(Guid userId, Guid id, UpdateCodingProblemRequest request, CancellationToken ct)
    {
        ValidateProblem(request.Title, request.Difficulty, request.Url);
        var problem = await db.CodingProblems.Include(p => p.Attempts).FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId, ct);
        if (problem == null) return null;
        problem.Title = request.Title.Trim(); problem.Difficulty = request.Difficulty.ToLowerInvariant(); problem.Platform = request.Platform?.Trim();
        problem.Url = request.Url?.Trim(); problem.Category = request.Category?.Trim(); problem.Description = request.Description; problem.Notes = request.Notes;
        await db.SaveChangesAsync(ct); return Map(problem);
    }

    public async Task<bool> DeleteAsync(Guid userId, Guid id, CancellationToken ct)
    {
        var gate = Gates.GetOrAdd(userId, _ => new(1, 1)); await gate.WaitAsync(ct);
        try
        {
            await using var transaction = await LockAsync(userId, id, ct);
            var problem = await db.CodingProblems.Include(p => p.Attempts).FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId, ct);
            if (problem == null) return false;
            db.ProblemAttempts.RemoveRange(problem.Attempts); db.CodingProblems.Remove(problem);
            await db.SaveChangesAsync(ct); if (transaction != null) await transaction.CommitAsync(ct); return true;
        }
        finally { gate.Release(); }
    }

    public async Task<CodingProblemDto?> RecordAsync(Guid userId, Guid id, SolveProblemRequest request, bool review, CancellationToken ct)
    {
        if (request.TimeSpentMinutes is < 1 or > 1440) throw new InvalidOperationException("Time spent must be between 1 and 1440 minutes.");
        if ((request.SolutionLanguage?.Length ?? 0) > 100 || (request.Notes?.Length ?? 0) > 5000) throw new InvalidOperationException("Use a language of at most 100 characters and notes of at most 5000 characters.");
        var gate = Gates.GetOrAdd(userId, _ => new(1, 1)); await gate.WaitAsync(ct);
        try
        {
            await using var transaction = await LockAsync(userId, id, ct);
            var problem = await db.CodingProblems.Include(p => p.Attempts).FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId, ct);
            if (problem == null) return null;
            if (!review && problem.IsSolved) { if (transaction != null) await transaction.CommitAsync(ct); return Map(problem); }
            if (review && problem.Attempts.Count == 0) throw new InvalidOperationException("Solve this problem before recording a review.");
            var day = await days.TodayAsync(userId, ct);
            var latestToday = problem.Attempts.Where(a => a.SolvedAt >= day.Start && a.SolvedAt < day.End).OrderByDescending(a => a.SolvedAt).FirstOrDefault();
            if (latestToday != null)
            {
                if ((!string.IsNullOrWhiteSpace(request.Notes) && request.Notes.Trim() != latestToday.Notes)
                    || (!string.IsNullOrWhiteSpace(request.SolutionLanguage) && request.SolutionLanguage.Trim() != latestToday.SolutionLanguage)
                    || (request.TimeSpentMinutes.HasValue && request.TimeSpentMinutes != latestToday.TimeSpentMinutes))
                    throw new InvalidOperationException("An attempt is already recorded for this problem today. Your existing notes and XP were kept. Try again on your next local day.");
                // A retry, or marking unsolved and solving again, must not award XP twice.
                problem.IsSolved = true; problem.SolvedAt = latestToday.SolvedAt;
                problem.SolutionLanguage = latestToday.SolutionLanguage; problem.TimeSpentMinutes = latestToday.TimeSpentMinutes;
                await db.SaveChangesAsync(ct); if (transaction != null) await transaction.CommitAsync(ct); return Map(problem);
            }
            var firstSolve = problem.Attempts.Count == 0;
            var xp = firstSolve ? problem.Difficulty switch { "medium" => 25, "hard" => 50, _ => 10 } : 5;
            if (firstSolve)
            {
                var timestamps = await db.ProblemAttempts.Where(a => a.Problem.UserId == userId).Select(a => a.SolvedAt).ToListAsync(ct);
                if (!timestamps.Any(t => t >= day.Start && t < day.End)) xp += 5;
                var dates = timestamps.Select(day.DateOf).Distinct().OrderByDescending(d => d).ToList();
                var streak = 0; var expected = dates.FirstOrDefault() == day.Date ? day.Date : day.Date.AddDays(-1);
                foreach (var date in dates) { if (date != expected) break; streak++; expected = expected.AddDays(-1); }
                xp += Math.Min(streak * 2, 20);
            }
            var now = DateTimeOffset.UtcNow;
            var attempt = new ProblemAttempt { ProblemId = id, SolvedAt = now, SolutionLanguage = request.SolutionLanguage?.Trim(), TimeSpentMinutes = request.TimeSpentMinutes, Notes = request.Notes?.Trim(), XpEarned = xp };
            db.ProblemAttempts.Add(attempt);
            problem.IsSolved = true; problem.SolvedAt = now; problem.SolutionLanguage = attempt.SolutionLanguage; problem.TimeSpentMinutes = attempt.TimeSpentMinutes;
            db.XpTransactions.Add(new() { UserId = userId, Amount = xp, Source = "coding", Description = (firstSolve ? "Solved '" : "Reviewed '") + problem.Title + "'", CreatedAt = now });
            await db.SaveChangesAsync(ct); if (transaction != null) await transaction.CommitAsync(ct); return Map(problem);
        }
        finally { gate.Release(); }
    }

    public async Task<CodingProblemDto?> UnsolveAsync(Guid userId, Guid id, CancellationToken ct)
    {
        var gate = Gates.GetOrAdd(userId, _ => new(1, 1)); await gate.WaitAsync(ct);
        try
        {
            await using var transaction = await LockAsync(userId, id, ct);
            var problem = await db.CodingProblems.Include(p => p.Attempts).FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId, ct);
            if (problem == null) return null;
            problem.IsSolved = false; problem.SolvedAt = null; problem.SolutionLanguage = null; problem.TimeSpentMinutes = null;
            await db.SaveChangesAsync(ct); if (transaction != null) await transaction.CommitAsync(ct); return Map(problem);
        }
        finally { gate.Release(); }
    }

    private static void ValidateProblem(string title, string difficulty, string? url)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Length > 300) throw new InvalidOperationException("Enter a problem title of at most 300 characters.");
        if (difficulty == null || !new[] { "easy", "medium", "hard" }.Contains(difficulty.ToLowerInvariant())) throw new InvalidOperationException("Choose Easy, Medium or Hard difficulty.");
        if (!string.IsNullOrWhiteSpace(url) && (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) || (uri.Scheme != "https" && uri.Scheme != "http"))) throw new InvalidOperationException("Use an http or https problem URL.");
    }

    public static CodingProblemDto Map(CodingProblem p) => new() { Id = p.Id, Title = p.Title, Platform = p.Platform, Url = p.Url, Difficulty = p.Difficulty, Category = p.Category,
        Description = p.Description, Notes = p.Notes, IsSolved = p.IsSolved, SolvedAt = p.SolvedAt, SolutionLanguage = p.SolutionLanguage, TimeSpentMinutes = p.TimeSpentMinutes, AttemptCount = p.Attempts.Count };
}
