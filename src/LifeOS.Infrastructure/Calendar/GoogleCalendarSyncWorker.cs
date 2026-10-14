using LifeOS.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
namespace LifeOS.Infrastructure.Calendar;
public class GoogleCalendarSyncWorker(IServiceScopeFactory scopes,ILogger<GoogleCalendarSyncWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer=new PeriodicTimer(TimeSpan.FromMinutes(5));
        while(await timer.WaitForNextTickAsync(stoppingToken)){
            try{using var scope=scopes.CreateScope();await scope.ServiceProvider.GetRequiredService<IGoogleCalendarService>().SyncAllAsync(stoppingToken);}
            catch(OperationCanceledException)when(stoppingToken.IsCancellationRequested){break;}
            catch(Exception){logger.LogWarning("Google calendar refresh could not complete.");}
        }
    }
}
