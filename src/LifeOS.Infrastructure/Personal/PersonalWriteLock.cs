using System.Collections.Concurrent;
using LifeOS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
namespace LifeOS.Infrastructure.Personal;
internal static class PersonalWriteLock
{
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> Gates = new();
    public static async Task<T> RunAsync<T>(AppDbContext db, Guid userId, Func<Task<T>> action, CancellationToken ct)
    {
        var gate = Gates.GetOrAdd(userId, _ => new(1, 1)); await gate.WaitAsync(ct);
        try
        {
            await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
            if (transaction != null) { var key = BitConverter.ToInt64(userId.ToByteArray(), 0) ^ 0x504552534F4EL; await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({key})", ct); }
            var result = await action(); await db.SaveChangesAsync(ct); if (transaction != null) await transaction.CommitAsync(ct); return result;
        }
        finally { gate.Release(); }
    }
}
