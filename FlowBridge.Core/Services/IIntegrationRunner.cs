using FlowBridge.Core.Models;

namespace FlowBridge.Core.Services;

public interface IIntegrationRunner
{
    Task<ExecutionRecord> RunAsync(int integrationId, int attemptNumber = 1, long? retryOfExecutionId = null, CancellationToken cancellationToken = default);
}
