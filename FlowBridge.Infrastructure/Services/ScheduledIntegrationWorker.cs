using FlowBridge.Core.Services;
using FlowBridge.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FlowBridge.Infrastructure.Services;

public sealed class ScheduledIntegrationWorker(IServiceScopeFactory scopeFactory, ILogger<ScheduledIntegrationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await RunDueIntegrationsAsync(stoppingToken); }
            catch (Exception ex) { logger.LogError(ex, "Scheduled integration cycle failed."); }
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }

    private async Task RunDueIntegrationsAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FlowBridgeDbContext>();
        var runner = scope.ServiceProvider.GetRequiredService<IIntegrationRunner>();
        var now = DateTime.UtcNow;
        var due = await db.Integrations.Where(x => x.IsEnabled && x.ScheduleEnabled && (x.NextRunUtc == null || x.NextRunUtc <= now)).ToListAsync(cancellationToken);
        foreach (var integration in due)
        {
            logger.LogInformation("Running scheduled integration {IntegrationName}", integration.Name);
            await runner.RunAsync(integration.Id, cancellationToken: cancellationToken);
            integration.NextRunUtc = DateTime.UtcNow.AddMinutes(Math.Max(1, integration.IntervalMinutes));
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
