using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.DataProtection;
using FlowBridge.Core.Models;
using FlowBridge.Core.Services;
using FlowBridge.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FlowBridge.Infrastructure.Services;

public sealed class IntegrationRunner(
    FlowBridgeDbContext db,
    IHttpClientFactory httpClientFactory,
    IDataProtectionProvider dataProtectionProvider) : IIntegrationRunner
{
    public async Task<ExecutionRecord> RunAsync(int integrationId, int attemptNumber = 1, long? retryOfExecutionId = null, CancellationToken cancellationToken = default)
    {
        var integration = await db.Integrations.SingleOrDefaultAsync(x => x.Id == integrationId, cancellationToken)
            ?? throw new InvalidOperationException("Integration not found.");

        if (!integration.IsEnabled)
            throw new InvalidOperationException("This integration is disabled.");

        var record = new ExecutionRecord
        {
            IntegrationDefinitionId = integration.Id,
            StartedUtc = DateTime.UtcNow,
            RequestBody = integration.BodyTemplate
            ,AttemptNumber = attemptNumber
            ,RetryOfExecutionId = retryOfExecutionId
        };
        var timer = Stopwatch.StartNew();

        try
        {
            var variables = BuildVariables(integration.VariablesJson);
            var expandedUrl = Expand(integration.Url, variables);
            var expandedBody = integration.ConnectorType.Equals("GraphQL", StringComparison.OrdinalIgnoreCase)
                ? BuildGraphQlBody(integration, variables)
                : Expand(integration.BodyTemplate, variables);
            record.RequestBody = expandedBody;
            var requestMethod = integration.ConnectorType.Equals("GraphQL", StringComparison.OrdinalIgnoreCase) ? "POST" : integration.Method;
            using var request = new HttpRequestMessage(new HttpMethod(requestMethod), expandedUrl);
            if (!string.IsNullOrWhiteSpace(expandedBody) && requestMethod is not "GET" and not "HEAD")
                request.Content = new StringContent(expandedBody, Encoding.UTF8, "application/json");

            var headers = JsonSerializer.Deserialize<Dictionary<string, string>>(integration.HeadersJson) ?? [];
            if (!string.IsNullOrWhiteSpace(integration.ProtectedHeadersJson))
            {
                var protector = dataProtectionProvider.CreateProtector("FlowBridge.SecretHeaders.v1");
                var secretJson = protector.Unprotect(integration.ProtectedHeadersJson);
                var secretHeaders = JsonSerializer.Deserialize<Dictionary<string, string>>(secretJson) ?? [];
                foreach (var secret in secretHeaders) headers[secret.Key] = secret.Value;
            }
            foreach (var header in headers)
            {
                var value = Expand(header.Value, variables);
                if (!request.Headers.TryAddWithoutValidation(header.Key, value))
                    request.Content?.Headers.TryAddWithoutValidation(header.Key, value);
            }

            using var response = await httpClientFactory.CreateClient("FlowBridge").SendAsync(request, cancellationToken);
            record.StatusCode = (int)response.StatusCode;
            record.ResponseBody = Limit(await response.Content.ReadAsStringAsync(cancellationToken));
            record.Succeeded = response.IsSuccessStatusCode;
            if (record.Succeeded && integration.ConnectorType.Equals("GraphQL", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    using var graphQlResponse = JsonDocument.Parse(record.ResponseBody);
                    if (graphQlResponse.RootElement.TryGetProperty("errors", out var errors) &&
                        errors.ValueKind == JsonValueKind.Array && errors.GetArrayLength() > 0)
                    {
                        record.Succeeded = false;
                        record.ErrorMessage = "GraphQL returned one or more errors. See the response body for details.";
                    }
                }
                catch (JsonException)
                {
                    record.Succeeded = false;
                    record.ErrorMessage = "GraphQL endpoint returned an invalid JSON response.";
                }
            }
            if (!record.Succeeded)
                record.ErrorMessage = string.IsNullOrWhiteSpace(record.ErrorMessage)
                    ? $"Remote endpoint returned HTTP {(int)response.StatusCode}."
                    : record.ErrorMessage;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            record.Succeeded = false;
            record.ErrorMessage = ex.Message;
        }
        finally
        {
            timer.Stop();
            record.CompletedUtc = DateTime.UtcNow;
            record.DurationMs = timer.ElapsedMilliseconds;
            integration.LastRunUtc = record.CompletedUtc;
            if (record.Succeeded)
            {
                record.RetryState = "Completed";
            }
            else if (attemptNumber <= integration.MaxRetries)
            {
                record.RetryState = "PendingRetry";
                var delay = integration.RetryDelaySeconds * Math.Pow(2, Math.Max(0, attemptNumber - 1));
                record.NextRetryUtc = DateTime.UtcNow.AddSeconds(Math.Min(delay, 86400));
            }
            else
            {
                record.RetryState = "DeadLetter";
            }
            db.Executions.Add(record);
            await db.SaveChangesAsync(CancellationToken.None);
        }

        return record;
    }

    private static string Limit(string value) => value.Length <= 100_000 ? value : value[..100_000];

    private static Dictionary<string, string> BuildVariables(string json)
    {
        var values = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? [];
        values["utcNow"] = DateTime.UtcNow.ToString("O");
        values["date"] = DateTime.UtcNow.ToString("yyyy-MM-dd");
        values["guid"] = Guid.NewGuid().ToString();
        return values;
    }

    private static string Expand(string template, Dictionary<string, string> values)
    {
        var result = template ?? "";
        foreach (var value in values)
            result = result.Replace("{{" + value.Key + "}}", value.Value, StringComparison.OrdinalIgnoreCase);
        return result;
    }

    private static string BuildGraphQlBody(IntegrationDefinition integration, Dictionary<string, string> values)
    {
        var query = Expand(integration.GraphQlQuery, values);
        var variablesJson = Expand(integration.GraphQlVariablesJson, values);
        JsonNode variablesNode;
        try { variablesNode = JsonNode.Parse(string.IsNullOrWhiteSpace(variablesJson) ? "{}" : variablesJson) ?? new JsonObject(); }
        catch (JsonException ex) { throw new InvalidOperationException("GraphQL variables are not valid JSON.", ex); }

        var payload = new JsonObject
        {
            ["query"] = query,
            ["variables"] = variablesNode
        };
        if (!string.IsNullOrWhiteSpace(integration.GraphQlOperationName))
            payload["operationName"] = Expand(integration.GraphQlOperationName, values);
        return payload.ToJsonString();
    }
}
