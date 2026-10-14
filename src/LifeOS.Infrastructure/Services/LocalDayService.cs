using LifeOS.Application.Interfaces;
using LifeOS.Application.Services;
using LifeOS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
namespace LifeOS.Infrastructure.Services;
public class LocalDayService(AppDbContext context) : ILocalDayService
{
    public async Task<LocalDay> TodayAsync(Guid userId, CancellationToken ct = default)
    {
        var zone = await context.UserProfiles.Where(p => p.UserId == userId)
            .Select(p => p.TimeZone).FirstOrDefaultAsync(ct);
        return LocalDay.At(DateTimeOffset.UtcNow, zone);
    }
}
