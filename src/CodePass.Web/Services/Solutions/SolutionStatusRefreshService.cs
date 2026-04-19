using Microsoft.Extensions.Hosting;

namespace CodePass.Web.Services.Solutions;

public sealed class SolutionStatusRefreshService : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;
}
