using Microsoft.EntityFrameworkCore;

namespace FlowBridge.Infrastructure.Data;

public static class DatabaseUpgrade
{
    public static async Task ApplyAsync(FlowBridgeDbContext db)
    {
        await db.Database.EnsureCreatedAsync();
        await AddColumnAsync(db, "VariablesJson", "TEXT NOT NULL DEFAULT '{}'");
        await AddColumnAsync(db, "ScheduleEnabled", "INTEGER NOT NULL DEFAULT 0");
        await AddColumnAsync(db, "IntervalMinutes", "INTEGER NOT NULL DEFAULT 15");
        await AddColumnAsync(db, "LastRunUtc", "TEXT NULL");
        await AddColumnAsync(db, "NextRunUtc", "TEXT NULL");
        await AddColumnAsync(db, "MaxRetries", "INTEGER NOT NULL DEFAULT 3");
        await AddColumnAsync(db, "RetryDelaySeconds", "INTEGER NOT NULL DEFAULT 30");
        await AddColumnAsync(db, "ConnectorType", "TEXT NOT NULL DEFAULT 'REST'");
        await AddColumnAsync(db, "GraphQlQuery", "TEXT NOT NULL DEFAULT ''");
        await AddColumnAsync(db, "GraphQlVariablesJson", "TEXT NOT NULL DEFAULT '{}'");
        await AddColumnAsync(db, "GraphQlOperationName", "TEXT NOT NULL DEFAULT ''");
        await AddColumnAsync(db, "ProtectedHeadersJson", "TEXT NOT NULL DEFAULT ''");
        await AddExecutionColumnAsync(db, "AttemptNumber", "INTEGER NOT NULL DEFAULT 1");
        await AddExecutionColumnAsync(db, "RetryOfExecutionId", "INTEGER NULL");
        await AddExecutionColumnAsync(db, "NextRetryUtc", "TEXT NULL");
        await AddExecutionColumnAsync(db, "RetryState", "TEXT NOT NULL DEFAULT 'Completed'");
    }

    private static async Task AddColumnAsync(FlowBridgeDbContext db, string name, string definition)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync();
        await using var check = connection.CreateCommand();
        check.CommandText = "PRAGMA table_info('Integrations');";
        await using var reader = await check.ExecuteReaderAsync();
        var exists = false;
        while (await reader.ReadAsync())
            if (string.Equals(reader.GetString(1), name, StringComparison.OrdinalIgnoreCase)) exists = true;
        await reader.CloseAsync();
        if (exists) return;
        await using var alter = connection.CreateCommand();
        alter.CommandText = $"ALTER TABLE Integrations ADD COLUMN {name} {definition};";
        await alter.ExecuteNonQueryAsync();
    }

    private static async Task AddExecutionColumnAsync(FlowBridgeDbContext db, string name, string definition)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync();
        await using var check = connection.CreateCommand();
        check.CommandText = "PRAGMA table_info('Executions');";
        await using var reader = await check.ExecuteReaderAsync();
        var exists = false;
        while (await reader.ReadAsync())
            if (string.Equals(reader.GetString(1), name, StringComparison.OrdinalIgnoreCase)) exists = true;
        await reader.CloseAsync();
        if (exists) return;
        await using var alter = connection.CreateCommand();
        alter.CommandText = $"ALTER TABLE Executions ADD COLUMN {name} {definition};";
        await alter.ExecuteNonQueryAsync();
    }
}
