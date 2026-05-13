using Microsoft.Data.SqlClient;
using System.Net.Sockets;
using System.Text.Json;
using DotNet.Testcontainers.Builders;
using MiniBus.Core.Contracts;
using MiniBus.Core.Headers;
using MiniBus.Core.Persistence;
using MiniBus.Core.Serialization;
using Testcontainers.MsSql;
using Xunit;

namespace MiniBus.Persistence.Sql.Tests;

public sealed class SqlServerIntegrationTests : IClassFixture<SqlServerIntegrationTests.SqlServerFixture>
{
    private const string ConnectionStringEnvironmentVariable = "MINIBUS_SQLSERVER_TEST_CONNECTION_STRING";
    private const string SqlServerImage = "mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04";
    private readonly SqlServerFixture _fixture;

    public SqlServerIntegrationTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
    }

    [SqlServerFact]
    public async Task SchemaScript_CanBeAppliedToSqlServer()
    {
        await using var database = await _fixture.CreateDatabaseAsync();

        var inboxExists = await database.ObjectExistsAsync("Inbox");
        var outboxExists = await database.ObjectExistsAsync("Outbox");

        Assert.True(inboxExists);
        Assert.True(outboxExists);
    }

    [SqlServerFact]
    public async Task Inbox_RecordsProcessedMessagesAndDetectsDuplicates()
    {
        await using var database = await _fixture.CreateDatabaseAsync();
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
        await using var database = await _fixture.CreateDatabaseAsync();
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
        await using var database = await _fixture.CreateDatabaseAsync();
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
        await using var database = await _fixture.CreateDatabaseAsync();
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
        await using var database = await _fixture.CreateDatabaseAsync();
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

    public sealed class SqlServerFixture : IAsyncLifetime
    {
        private readonly string? _externalConnectionString;
        private MsSqlContainer? _container;
        private Exception? _startupException;

        public SqlServerFixture()
        {
            _externalConnectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        }

        public async Task InitializeAsync()
        {
            if (!string.IsNullOrWhiteSpace(_externalConnectionString))
            {
                return;
            }

            try
            {
                _container = new MsSqlBuilder()
                    .WithImage(SqlServerImage)
                    .WithCreateParameterModifier(parameters =>
                    {
                        parameters.Platform = "linux/amd64";
                    })
                    .Build();

                await _container.StartAsync();
            }
            catch (Exception exception)
            {
                _startupException = exception;
            }
        }

        public async Task DisposeAsync()
        {
            if (_container is not null)
            {
                await _container.DisposeAsync();
            }
        }

        internal async Task<SqlServerTestDatabase> CreateDatabaseAsync()
        {
            if (_startupException is not null)
            {
                throw new InvalidOperationException(
                    $"SQL Server Testcontainers startup failed for {SqlServerImage} on linux/amd64. " +
                    "On Apple Silicon, ensure Docker Desktop can run amd64 Linux containers. " +
                    $"Alternatively set {ConnectionStringEnvironmentVariable}. " +
                    $"Original error: {_startupException.Message}");
            }

            var connectionString = !string.IsNullOrWhiteSpace(_externalConnectionString)
                ? _externalConnectionString
                : _container?.GetConnectionString();

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    $"Set {ConnectionStringEnvironmentVariable} or enable Docker to run SQL Server-backed MiniBus persistence tests.");
            }

            var database = new SqlServerTestDatabase(
                connectionString,
                $"MiniBusTest_{Guid.NewGuid():N}");
            await database.ApplySchemaAsync();
            return database;
        }
    }

    internal sealed class SqlServerTestDatabase : IAsyncDisposable
    {
        private readonly MiniBusSqlPersistenceOptions _options;
        private readonly SqlOutboxOperationSerializer _operationSerializer;

        internal SqlServerTestDatabase(string connectionString, string schemaName)
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

        public async Task ApplySchemaAsync()
        {
            var script = File.ReadAllText(Path.GetFullPath(
                "../../../../../src/MiniBus.Persistence.Sql/Schema/001-inbox-outbox.sql",
                AppContext.BaseDirectory));
            script = script
                .Replace("SCHEMA_ID(N'MiniBus')", $"SCHEMA_ID(N'{SchemaName}')", StringComparison.Ordinal)
                .Replace("CREATE SCHEMA MiniBus", $"CREATE SCHEMA [{SchemaName}]", StringComparison.Ordinal)
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
            Timeout = 180_000;

            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable))
                && !DockerSocketIsReachable())
            {
                Skip = "Docker is not reachable, and MINIBUS_SQLSERVER_TEST_CONNECTION_STRING is not set. " +
                       "Start Docker Desktop with linux/amd64 container support or configure an external SQL Server/Azure SQL test connection string.";
            }
        }

        private static bool DockerSocketIsReachable()
        {
            if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DOCKER_HOST")))
            {
                return true;
            }

            return UnixSocketIsReachable("/var/run/docker.sock")
                   || UnixSocketIsReachable(Path.Combine(
                       Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                       ".docker",
                       "run",
                       "docker.sock"));
        }

        private static bool UnixSocketIsReachable(string path)
        {
            if (!File.Exists(path))
            {
                return false;
            }

            try
            {
                using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                var connectTask = socket.ConnectAsync(new UnixDomainSocketEndPoint(path));
                return connectTask.Wait(TimeSpan.FromMilliseconds(250)) && socket.Connected;
            }
            catch
            {
                return false;
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
