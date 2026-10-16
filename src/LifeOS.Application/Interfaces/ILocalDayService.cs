using LifeOS.Application.Services;
namespace LifeOS.Application.Interfaces;
public interface ILocalDayService
{
    Task<LocalDay> TodayAsync(Guid userId, CancellationToken ct = default);
}
