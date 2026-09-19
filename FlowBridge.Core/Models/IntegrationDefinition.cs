namespace FlowBridge.Core.Models;

public sealed class IntegrationDefinition
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string ConnectorType { get; set; } = "REST";
    public string Method { get; set; } = "GET";
    public string Url { get; set; } = "";
    public string HeadersJson { get; set; } = "{}";
    public string ProtectedHeadersJson { get; set; } = "";
    public string BodyTemplate { get; set; } = "";
    public string VariablesJson { get; set; } = "{}";
    public string GraphQlQuery { get; set; } = "";
    public string GraphQlVariablesJson { get; set; } = "{}";
    public string GraphQlOperationName { get; set; } = "";
    public bool IsEnabled { get; set; } = true;
    public bool ScheduleEnabled { get; set; }
    public int IntervalMinutes { get; set; } = 15;
    public int MaxRetries { get; set; } = 3;
    public int RetryDelaySeconds { get; set; } = 30;
    public DateTime? LastRunUtc { get; set; }
    public DateTime? NextRunUtc { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
    public List<ExecutionRecord> Executions { get; set; } = [];
}
