using LifeOS.Application.Interfaces;
using LifeOS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
namespace LifeOS.Infrastructure.Planning;
public class ReminderWorker(IServiceScopeFactory scopes,ILogger<ReminderWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer=new PeriodicTimer(TimeSpan.FromMinutes(5));
        while(await timer.WaitForNextTickAsync(ct)){
            try {
            using var root=scopes.CreateScope();var ids=await root.ServiceProvider.GetRequiredService<AppDbContext>().Users.AsNoTracking().Select(u=>u.Id).ToListAsync(ct);
            foreach(var id in ids){try{using var scope=scopes.CreateScope();var plan=await scope.ServiceProvider.GetRequiredService<IPlanningService>().TodayAsync(id,ct);await scope.ServiceProvider.GetRequiredService<IReminderService>().RemindAsync(id,plan,ct);}catch(OperationCanceledException)when(ct.IsCancellationRequested){return;}catch(Exception){logger.LogWarning("Daily reminder refresh failed for an account.");}}
            }catch(OperationCanceledException)when(ct.IsCancellationRequested){return;}catch(Exception){logger.LogWarning("Daily reminder refresh could not access the database.");}
        }
    }
}
