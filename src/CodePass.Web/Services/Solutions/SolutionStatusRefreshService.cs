using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CodePass.Web.Services.Solutions;

public sealed class SolutionStatusRefreshService(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<SolutionStatusRefreshService> logger) : BackgroundService
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(RefreshInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RefreshStatusesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while refreshing registered solution statuses.");
            }

            if (!await timer.WaitForNextTickAsync(stoppingToken))
            {
                break;
            }
        }
    }

    private async Task RefreshStatusesAsync(CancellationToken cancellationToken)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var registeredSolutionService = scope.ServiceProvider.GetRequiredService<IRegisteredSolutionService>();
        await registeredSolutionService.RefreshAllAsync(cancellationToken);
    }
}
