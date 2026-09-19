namespace FlowBridge.Core.Models;

public sealed class ExecutionRecord
{
    public long Id { get; set; }
    public int IntegrationDefinitionId { get; set; }
    public IntegrationDefinition? IntegrationDefinition { get; set; }
    public DateTime StartedUtc { get; set; }
    public DateTime CompletedUtc { get; set; }
    public long DurationMs { get; set; }
    public int? StatusCode { get; set; }
    public bool Succeeded { get; set; }
    public string RequestBody { get; set; } = "";
    public string ResponseBody { get; set; } = "";
    public string ErrorMessage { get; set; } = "";
    public int AttemptNumber { get; set; } = 1;
    public long? RetryOfExecutionId { get; set; }
    public DateTime? NextRetryUtc { get; set; }
    public string RetryState { get; set; } = "Completed";
}
