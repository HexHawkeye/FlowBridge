using FlowBridge.Core.Services;
using FlowBridge.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FlowBridge.Infrastructure.Services;

public sealed class RetryWorker(IServiceScopeFactory scopeFactory, ILogger<RetryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await ProcessRetriesAsync(stoppingToken); }
            catch (Exception ex) { logger.LogError(ex, "Retry processing cycle failed."); }
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }

    private async Task ProcessRetriesAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FlowBridgeDbContext>();
        var runner = scope.ServiceProvider.GetRequiredService<IIntegrationRunner>();
        var now = DateTime.UtcNow;
        var due = await db.Executions
            .Where(x => x.RetryState == "PendingRetry" && x.NextRetryUtc <= now)
            .OrderBy(x => x.NextRetryUtc).Take(20).ToListAsync(cancellationToken);

        foreach (var failed in due)
        {
            failed.RetryState = "Retried";
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Retrying execution {ExecutionId}, attempt {Attempt}", failed.Id, failed.AttemptNumber + 1);
            await runner.RunAsync(failed.IntegrationDefinitionId, failed.AttemptNumber + 1,
                failed.RetryOfExecutionId ?? failed.Id, cancellationToken);
        }
    }
}
