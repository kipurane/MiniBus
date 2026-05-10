using Microsoft.Data.SqlClient;
using System.Text.Json;
using MiniBus.Core.Contracts;
using MiniBus.Core.Headers;
using MiniBus.Core.Persistence;
using MiniBus.Core.Serialization;
using Xunit;

namespace MiniBus.Persistence.Sql.Tests;

public sealed class SqlServerIntegrationTests
{
    private const string ConnectionStringEnvironmentVariable = "MINIBUS_SQLSERVER_TEST_CONNECTION_STRING";

    [SqlServerFact]
    public async Task SchemaScript_CanBeAppliedToSqlServer()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync();

        var inboxExists = await database.ObjectExistsAsync("Inbox");
        var outboxExists = await database.ObjectExistsAsync("Outbox");

        Assert.True(inboxExists);
        Assert.True(outboxExists);
    }

    [SqlServerFact]
    public async Task Inbox_RecordsProcessedMessagesAndDetectsDuplicates()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync();
        await using var session = await database.CreateSessionAsync();
        var message = database.CreateInboxMessage("message-1");

        Assert.False(await session.IsProcessedAsync(message));

        await session.CommitAsync(message, Array.Empty<MiniBusOutboxOperation>());

        await using var verificationSession = await database.CreateSessionAsync();
        Assert.True(await verificationSession.IsProcessedAsync(message));
    }

    [SqlServerFact]
    public async Task Commit_CapturesOutboxOperations()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync();
        await using var session = await database.CreateSessionAsync();
        var operation = CreateOutboxOperation();

        await session.CommitAsync(
            database.CreateInboxMessage("message-with-outbox"),
            new[] { operation });

        await using var connection = database.CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT OperationKind, MessageType, HeadersJson
            FROM {database.OutboxTableName};
            """;

        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(MiniBusOutboxOperationKind.Send.ToString(), reader.GetString(0));
        Assert.Contains(nameof(TestCommand), reader.GetString(1), StringComparison.Ordinal);
        Assert.Contains("correlation-1", reader.GetString(2), StringComparison.Ordinal);
        Assert.False(await reader.ReadAsync());
    }

    [SqlServerFact]
    public async Task OutboxStore_ClaimsAndUpdatesDispatchState()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync();
        await using var session = await database.CreateSessionAsync();

        await session.CommitAsync(
            database.CreateInboxMessage("message-for-claim"),
            new[] { CreateOutboxOperation() });

        var store = database.CreateOutboxStore();
        var operations = await store.ClaimPendingAsync(batchSize: 10);
        var operation = Assert.Single(operations);

        Assert.Equal(1, operation.AttemptCount);

        await store.MarkDispatchedAsync(operation.Id);
        var dispatchedUtc = await database.QueryScalarAsync<DateTimeOffset?>(
            $"SELECT DispatchedUtc FROM {database.OutboxTableName} WHERE Id = @Id;",
            operation.Id);

        Assert.NotNull(dispatchedUtc);
    }

    [SqlServerFact]
    public async Task OutboxStore_RecordsFailedDispatchMetadataAndLeavesOperationRetryable()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync();
        await using var session = await database.CreateSessionAsync();

        await session.CommitAsync(
            database.CreateInboxMessage("message-for-failure"),
            new[] { CreateOutboxOperation() });

        var store = database.CreateOutboxStore();
        var operation = Assert.Single(await store.ClaimPendingAsync(batchSize: 10));

        await store.MarkFailedAsync(operation.Id, new InvalidOperationException("transport failed"));

        await using var connection = database.CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT AttemptCount, LastError, ClaimedUtc
            FROM {database.OutboxTableName}
            WHERE Id = @Id;
            """;
        command.Parameters.Add(new SqlParameter("@Id", operation.Id));

        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(1, reader.GetInt32(0));
        Assert.Contains("transport failed", reader.GetString(1), StringComparison.Ordinal);
        Assert.True(reader.IsDBNull(2));
    }

    [SqlServerFact]
    public async Task Commit_RollsBackInboxRecordWhenOutboxInsertFails()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync();
        await database.DropOutboxTableAsync();

        await using var session = await database.CreateSessionAsync();
        var inboxMessage = database.CreateInboxMessage("message-rollback");

        await Assert.ThrowsAsync<SqlException>(() => session.CommitAsync(
            inboxMessage,
            new[] { CreateOutboxOperation() }));

        await using var verificationSession = await database.CreateSessionAsync();
        Assert.False(await verificationSession.IsProcessedAsync(inboxMessage));
    }

    private static MiniBusOutboxOperation CreateOutboxOperation()
    {
        return new MiniBusOutboxOperation(
            MiniBusOutboxOperationKind.Send,
            new TestCommand(Guid.NewGuid()),
            typeof(TestCommand),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [MiniBusHeaderNames.CorrelationId] = "correlation-1",
                [MiniBusHeaderNames.CausationId] = "message-1"
            },
            DueTime: null);
    }

    private sealed record TestCommand(Guid Id) : ICommand;

    private sealed class SqlServerTestDatabase : IAsyncDisposable
    {
        private readonly MiniBusSqlPersistenceOptions _options;
        private readonly SqlOutboxOperationSerializer _operationSerializer;

        private SqlServerTestDatabase(string connectionString, string schemaName)
        {
            ConnectionString = connectionString;
            SchemaName = schemaName;
            InboxTableName = $"[{SchemaName}].[Inbox]";
            OutboxTableName = $"[{SchemaName}].[Outbox]";
            _options = new MiniBusSqlPersistenceOptions
            {
                SchemaName = SchemaName,
                ConnectionFactory = CreateConnection
            };
            _operationSerializer = new SqlOutboxOperationSerializer(new JsonMessageSerializer());
        }

        public string ConnectionString { get; }

        public string SchemaName { get; }

        public string InboxTableName { get; }

        public string OutboxTableName { get; }

        public static async Task<SqlServerTestDatabase> CreateAsync()
        {
            var connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    $"Set {ConnectionStringEnvironmentVariable} to run SQL Server-backed MiniBus persistence tests.");
            }

            var database = new SqlServerTestDatabase(
                connectionString,
                $"MiniBusTest_{Guid.NewGuid():N}");
            await database.ApplySchemaAsync();
            return database;
        }

        public SqlConnection CreateConnection()
        {
            return new SqlConnection(ConnectionString);
        }

        public async ValueTask<IMiniBusPersistenceSession> CreateSessionAsync()
        {
            var factory = new SqlMiniBusPersistenceSessionFactory(_options, _operationSerializer);
            return await factory.CreateAsync();
        }

        public ISqlMiniBusOutboxStore CreateOutboxStore()
        {
            return new SqlMiniBusOutboxStore(_options, _operationSerializer);
        }

        public MiniBusInboxMessage CreateInboxMessage(string messageId)
        {
            return new MiniBusInboxMessage(
                "Billing",
                messageId,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [MiniBusHeaderNames.CorrelationId] = "correlation-1"
                },
                DateTimeOffset.UtcNow);
        }

        public async Task<bool> ObjectExistsAsync(string tableName)
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT OBJECT_ID(@ObjectName, N'U');";
            command.Parameters.Add(new SqlParameter("@ObjectName", $"{SchemaName}.{tableName}"));

            return await command.ExecuteScalarAsync() is not DBNull and not null;
        }

        public async Task<T?> QueryScalarAsync<T>(string commandText, Guid id)
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = commandText;
            command.Parameters.Add(new SqlParameter("@Id", id));

            var value = await command.ExecuteScalarAsync();

            if (value is null or DBNull)
            {
                return default;
            }

            return (T)value;
        }

        public async Task DropOutboxTableAsync()
        {
            await ExecuteNonQueryAsync($"DROP TABLE {OutboxTableName};");
        }

        public async ValueTask DisposeAsync()
        {
            await ExecuteNonQueryAsync($"""
                IF OBJECT_ID(N'{SchemaName}.Outbox', N'U') IS NOT NULL
                    DROP TABLE {OutboxTableName};

                IF OBJECT_ID(N'{SchemaName}.Inbox', N'U') IS NOT NULL
                    DROP TABLE {InboxTableName};

                IF SCHEMA_ID(N'{SchemaName}') IS NOT NULL
                    EXEC(N'DROP SCHEMA [{SchemaName}]');
                """);
        }

        private async Task ApplySchemaAsync()
        {
            var script = File.ReadAllText(Path.GetFullPath(
                "../../../../../src/MiniBus.Persistence.Sql/Schema/001-inbox-outbox.sql",
                AppContext.BaseDirectory));
            script = script
                .Replace("N'MiniBus'", $"N'{SchemaName}'", StringComparison.Ordinal)
                .Replace("MiniBus.", $"{SchemaName}.", StringComparison.Ordinal);

            await ExecuteNonQueryAsync(script);
        }

        private async Task ExecuteNonQueryAsync(string commandText)
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = commandText;
            await command.ExecuteNonQueryAsync();
        }
    }

    private sealed class SqlServerFactAttribute : FactAttribute
    {
        public SqlServerFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable)))
            {
                Skip = $"Set {ConnectionStringEnvironmentVariable} to run SQL Server-backed MiniBus persistence tests.";
            }
        }
    }

    private sealed class JsonMessageSerializer : IMessageSerializer
    {
        public BinaryData Serialize(object message, Type messageType)
        {
            return new BinaryData(JsonSerializer.SerializeToUtf8Bytes(message, messageType));
        }

        public object Deserialize(BinaryData body, Type messageType)
        {
            throw new NotSupportedException();
        }
    }
}
