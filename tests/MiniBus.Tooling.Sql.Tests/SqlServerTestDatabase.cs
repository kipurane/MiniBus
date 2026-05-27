using System.Text.Json;
using Microsoft.Data.SqlClient;
using MiniBus.Tooling.Sql;

namespace MiniBus.Tooling.Sql.Tests;

internal sealed class SqlServerTestDatabase : IAsyncDisposable
{
    public SqlServerTestDatabase(string connectionString, string schemaName)
    {
        ConnectionString = connectionString;
        SchemaName = schemaName;
    }

    public string ConnectionString { get; }

    public string SchemaName { get; }

    public SqlMiniBusToolingReader CreateReader()
    {
        return new SqlMiniBusToolingReader(new MiniBusSqlToolingOptions
        {
            ConnectionString = ConnectionString,
            SchemaName = SchemaName
        });
    }

    public SqlMiniBusToolingReader CreateReader(Action connectionCreated)
    {
        return new SqlMiniBusToolingReader(new MiniBusSqlToolingOptions
        {
            ConnectionFactory = () =>
            {
                connectionCreated();
                return new SqlConnection(ConnectionString);
            },
            SchemaName = SchemaName
        });
    }

    public async Task InsertInboxAsync(
        string endpointName,
        string messageId,
        string correlationId,
        DateTimeOffset? processedUtc = null)
    {
        await ExecuteNonQueryAsync($"""
            INSERT INTO [{SchemaName}].[Inbox]
                (EndpointName, MessageId, ProcessedUtc, HeadersJson, CorrelationId)
            VALUES
                (@EndpointName, @MessageId, @ProcessedUtc, @HeadersJson, @CorrelationId);
            """,
            Parameter("@EndpointName", endpointName),
            Parameter("@MessageId", messageId),
            Parameter("@ProcessedUtc", processedUtc ?? DateTimeOffset.UtcNow),
            Parameter("@HeadersJson", HeadersJson(correlationId)),
            Parameter("@CorrelationId", correlationId));
    }

    public async Task InsertOutboxAsync(
        string endpointName,
        string incomingMessageId,
        string outgoingMessageId,
        string operationKind,
        string messageType,
        string correlationId,
        DateTimeOffset? dispatchedUtc,
        string? lastError,
        DateTimeOffset? createdUtc = null)
    {
        await ExecuteNonQueryAsync($"""
            INSERT INTO [{SchemaName}].[Outbox]
                (Id, OutgoingMessageId, EndpointName, IncomingMessageId, OperationKind, MessageType, Body, HeadersJson, CorrelationId, DueTime, CreatedUtc, ClaimedUtc, DispatchedUtc, AttemptCount, LastError)
            VALUES
                (@Id, @OutgoingMessageId, @EndpointName, @IncomingMessageId, @OperationKind, @MessageType, @Body, @HeadersJson, @CorrelationId, NULL, @CreatedUtc, NULL, @DispatchedUtc, @AttemptCount, @LastError);
            """,
            Parameter("@Id", Guid.NewGuid()),
            Parameter("@OutgoingMessageId", outgoingMessageId),
            Parameter("@EndpointName", endpointName),
            Parameter("@IncomingMessageId", incomingMessageId),
            Parameter("@OperationKind", operationKind),
            Parameter("@MessageType", messageType),
            Parameter("@Body", Array.Empty<byte>()),
            Parameter("@HeadersJson", HeadersJson(correlationId)),
            Parameter("@CorrelationId", correlationId),
            Parameter("@CreatedUtc", createdUtc ?? DateTimeOffset.UtcNow),
            Parameter("@DispatchedUtc", dispatchedUtc),
            Parameter("@AttemptCount", lastError is null ? 0 : 1),
            Parameter("@LastError", lastError));
    }

    public async Task InsertInboxRawHeadersAsync(
        string endpointName,
        string messageId,
        string headersJson,
        string? correlationId = null)
    {
        await ExecuteNonQueryAsync($"""
            INSERT INTO [{SchemaName}].[Inbox]
                (EndpointName, MessageId, ProcessedUtc, HeadersJson, CorrelationId)
            VALUES
                (@EndpointName, @MessageId, @ProcessedUtc, @HeadersJson, @CorrelationId);
            """,
            Parameter("@EndpointName", endpointName),
            Parameter("@MessageId", messageId),
            Parameter("@ProcessedUtc", DateTimeOffset.UtcNow),
            Parameter("@HeadersJson", headersJson),
            Parameter("@CorrelationId", correlationId));
    }

    public async Task InsertOutboxRawHeadersAsync(
        string endpointName,
        string incomingMessageId,
        string outgoingMessageId,
        string headersJson,
        string? correlationId = null)
    {
        await ExecuteNonQueryAsync($"""
            INSERT INTO [{SchemaName}].[Outbox]
                (Id, OutgoingMessageId, EndpointName, IncomingMessageId, OperationKind, MessageType, Body, HeadersJson, CorrelationId, DueTime, CreatedUtc, ClaimedUtc, DispatchedUtc, AttemptCount, LastError)
            VALUES
                (@Id, @OutgoingMessageId, @EndpointName, @IncomingMessageId, @OperationKind, @MessageType, @Body, @HeadersJson, @CorrelationId, NULL, @CreatedUtc, NULL, NULL, 0, NULL);
            """,
            Parameter("@Id", Guid.NewGuid()),
            Parameter("@OutgoingMessageId", outgoingMessageId),
            Parameter("@EndpointName", endpointName),
            Parameter("@IncomingMessageId", incomingMessageId),
            Parameter("@OperationKind", "Send"),
            Parameter("@MessageType", "Contracts.CreateInvoice"),
            Parameter("@Body", Array.Empty<byte>()),
            Parameter("@HeadersJson", headersJson),
            Parameter("@CorrelationId", correlationId ?? ExtractCorrelationId(headersJson)),
            Parameter("@CreatedUtc", DateTimeOffset.UtcNow));
    }

    public async Task<bool> OutboxCorrelationIndexExistsAsync()
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT 1
            FROM sys.indexes
            WHERE name = N'IX_MiniBus_Outbox_CorrelationId'
              AND object_id = OBJECT_ID(@OutboxTableName, N'U');
            """;
        command.Parameters.Add(Parameter("@OutboxTableName", $"{SchemaName}.Outbox"));
        return await command.ExecuteScalarAsync() is not null;
    }

    public async Task InsertSagaAsync(
        string dataType,
        string correlationId,
        bool isCompleted,
        DateTimeOffset? updatedUtc = null)
    {
        var updated = updatedUtc ?? DateTimeOffset.UtcNow;
        await ExecuteNonQueryAsync($"""
            INSERT INTO [{SchemaName}].[Sagas]
                (Id, DataType, CorrelationId, Data, IsCompleted, CreatedUtc, UpdatedUtc, CompletedUtc)
            VALUES
                (@Id, @DataType, @CorrelationId, @Data, @IsCompleted, @CreatedUtc, @UpdatedUtc, @CompletedUtc);
            """,
            Parameter("@Id", Guid.NewGuid()),
            Parameter("@DataType", dataType),
            Parameter("@CorrelationId", correlationId),
            Parameter("@Data", Array.Empty<byte>()),
            Parameter("@IsCompleted", isCompleted),
            Parameter("@CreatedUtc", updated.AddMinutes(-5)),
            Parameter("@UpdatedUtc", updated),
            Parameter("@CompletedUtc", isCompleted ? updated : null));
    }

    public async Task AllowNullHeadersAsync()
    {
        await ExecuteNonQueryAsync($"""
            ALTER TABLE [{SchemaName}].[Inbox] ALTER COLUMN HeadersJson nvarchar(max) NULL;
            ALTER TABLE [{SchemaName}].[Outbox] ALTER COLUMN HeadersJson nvarchar(max) NULL;
            """);
    }

    public async Task SetInboxHeadersNullAsync(
        string endpointName,
        string messageId)
    {
        await ExecuteNonQueryAsync($"""
            UPDATE [{SchemaName}].[Inbox]
            SET HeadersJson = NULL
            WHERE EndpointName = @EndpointName
              AND MessageId = @MessageId;
            """,
            Parameter("@EndpointName", endpointName),
            Parameter("@MessageId", messageId));
    }

    public async Task SetOutboxHeadersNullAsync(string outgoingMessageId)
    {
        await ExecuteNonQueryAsync($"""
            UPDATE [{SchemaName}].[Outbox]
            SET HeadersJson = NULL
            WHERE OutgoingMessageId = @OutgoingMessageId;
            """,
            Parameter("@OutgoingMessageId", outgoingMessageId));
    }

    public async Task ApplySchemaAsync()
    {
        var schemaDirectory = Path.Combine(AppContext.BaseDirectory, "MiniBusSqlSchema");
        if (!Directory.Exists(schemaDirectory))
        {
            throw new DirectoryNotFoundException(
                $"MiniBus SQL schema scripts were not copied to the test output directory: {schemaDirectory}");
        }

        foreach (var scriptPath in Directory.GetFiles(schemaDirectory, "*.sql").Order(StringComparer.Ordinal))
        {
            var script = await File.ReadAllTextAsync(scriptPath);
            script = RewriteSchemaScript(script, SchemaName);

            await ExecuteNonQueryAsync(script);
        }
    }

    internal static string RewriteSchemaScript(string script, string schemaName)
    {
        var schemaIdentifier = QuoteIdentifier(schemaName);
        var schemaLiteral = EscapeSqlStringLiteral(schemaName);
        var inboxIdentifier = $"{schemaIdentifier}.[Inbox]";
        var outboxIdentifier = $"{schemaIdentifier}.[Outbox]";
        var sagaIdentifier = $"{schemaIdentifier}.[Sagas]";
        var inboxLiteral = EscapeSqlStringLiteral(inboxIdentifier);
        var outboxLiteral = EscapeSqlStringLiteral(outboxIdentifier);
        var sagaLiteral = EscapeSqlStringLiteral(sagaIdentifier);

        return script
            .Replace("CREATE SCHEMA MiniBus", $"CREATE SCHEMA {schemaIdentifier}", StringComparison.Ordinal)
            .Replace("N'MiniBus.Inbox'", $"N'{inboxLiteral}'", StringComparison.Ordinal)
            .Replace("N'MiniBus.Outbox'", $"N'{outboxLiteral}'", StringComparison.Ordinal)
            .Replace("N'MiniBus.Sagas'", $"N'{sagaLiteral}'", StringComparison.Ordinal)
            .Replace("N'MiniBus'", $"N'{schemaLiteral}'", StringComparison.Ordinal)
            .Replace("MiniBus.Inbox", inboxIdentifier, StringComparison.Ordinal)
            .Replace("MiniBus.Outbox", outboxIdentifier, StringComparison.Ordinal)
            .Replace("MiniBus.Sagas", sagaIdentifier, StringComparison.Ordinal);
    }

    public async ValueTask DisposeAsync()
    {
        var schemaIdentifier = QuoteIdentifier(SchemaName);
        var schemaLiteral = EscapeSqlStringLiteral(SchemaName);
        var inboxLiteral = EscapeSqlStringLiteral($"{schemaIdentifier}.[Inbox]");
        var outboxLiteral = EscapeSqlStringLiteral($"{schemaIdentifier}.[Outbox]");
        var sagaLiteral = EscapeSqlStringLiteral($"{schemaIdentifier}.[Sagas]");
        var dropSchemaStatement = EscapeSqlStringLiteral($"DROP SCHEMA {schemaIdentifier}");

        await ExecuteNonQueryAsync($"""
            IF OBJECT_ID(N'{outboxLiteral}', N'U') IS NOT NULL
                DROP TABLE {schemaIdentifier}.[Outbox];

            IF OBJECT_ID(N'{sagaLiteral}', N'U') IS NOT NULL
                DROP TABLE {schemaIdentifier}.[Sagas];

            IF OBJECT_ID(N'{inboxLiteral}', N'U') IS NOT NULL
                DROP TABLE {schemaIdentifier}.[Inbox];

            IF SCHEMA_ID(N'{schemaLiteral}') IS NOT NULL
                EXEC(N'{dropSchemaStatement}');
            """);
    }

    private async Task ExecuteNonQueryAsync(string commandText, params SqlParameter[] parameters)
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        command.Parameters.AddRange(parameters);
        await command.ExecuteNonQueryAsync();
    }

    private static SqlParameter Parameter(string name, object? value)
    {
        return new SqlParameter(name, value ?? DBNull.Value);
    }

    private static string QuoteIdentifier(string value)
    {
        return $"[{value.Replace("]", "]]", StringComparison.Ordinal)}]";
    }

    private static string EscapeSqlStringLiteral(string value)
    {
        return value.Replace("'", "''", StringComparison.Ordinal);
    }

    private static string HeadersJson(string correlationId)
    {
        return JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["MiniBus.CorrelationId"] = correlationId
        });
    }

    private static string? ExtractCorrelationId(string headersJson)
    {
        try
        {
            var headers = JsonSerializer.Deserialize<Dictionary<string, string>>(headersJson);
            return headers is not null
                   && headers.TryGetValue("MiniBus.CorrelationId", out var correlationId)
                ? correlationId
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
