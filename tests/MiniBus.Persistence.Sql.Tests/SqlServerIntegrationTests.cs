using Microsoft.Data.SqlClient;
using System.Collections.Concurrent;
using System.Data.Common;
using System.Net.Sockets;
using System.Text.Json;
using DotNet.Testcontainers.Builders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MiniBus.Core.Context;
using MiniBus.Core.Contracts;
using MiniBus.Core.Headers;
using MiniBus.Core.Persistence;
using MiniBus.Core.Sagas;
using MiniBus.Core.Serialization;
using MiniBus.Persistence.Sql.DependencyInjection;
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
        var sagasExists = await database.ObjectExistsAsync("Sagas");

        Assert.True(inboxExists);
        Assert.True(outboxExists);
        Assert.True(sagasExists);
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
            SELECT OperationKind, MessageType, HeadersJson, OutgoingMessageId
            FROM {database.OutboxTableName};
            """;

        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(MiniBusOutboxOperationKind.Send.ToString(), reader.GetString(0));
        Assert.Contains(nameof(TestCommand), reader.GetString(1), StringComparison.Ordinal);
        Assert.Contains("correlation-1", reader.GetString(2), StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(reader.GetString(3)));
        Assert.False(await reader.ReadAsync());
    }

    [SqlServerFact]
    public async Task Commit_CanUseApplicationOwnedTransaction()
    {
        await using var database = await _fixture.CreateDatabaseAsync();
        await database.CreateBusinessTableAsync();
        await using var connection = database.CreateConnection();
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        var factory = database.CreateSessionFactory();
        await using var session = factory.CreateForTransaction(connection, transaction);

        await database.InsertBusinessRecordAsync(connection, transaction, id: 1);
        await session.CommitAsync(
            database.CreateInboxMessage("message-shared-transaction"),
            new[] { CreateOutboxOperation() });

        await transaction.CommitAsync();

        Assert.Equal(1, await database.CountRowsAsync(database.BusinessTableName));
        Assert.Equal(1, await database.CountRowsAsync(database.InboxTableName));
        Assert.Equal(1, await database.CountRowsAsync(database.OutboxTableName));
    }

    [SqlServerFact]
    public async Task Commit_DoesNotCompleteApplicationOwnedTransaction()
    {
        await using var database = await _fixture.CreateDatabaseAsync();
        await database.CreateBusinessTableAsync();
        await using var connection = database.CreateConnection();
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        var factory = database.CreateSessionFactory();
        await using var session = factory.CreateForTransaction(connection, transaction);

        await database.InsertBusinessRecordAsync(connection, transaction, id: 1);
        await session.CommitAsync(
            database.CreateInboxMessage("message-shared-rollback"),
            new[] { CreateOutboxOperation() });

        await transaction.RollbackAsync();

        Assert.Equal(0, await database.CountRowsAsync(database.BusinessTableName));
        Assert.Equal(0, await database.CountRowsAsync(database.InboxTableName));
        Assert.Equal(0, await database.CountRowsAsync(database.OutboxTableName));
    }

    [SqlServerFact]
    public async Task CreateForTransaction_RejectsInvalidTransactionOwnership()
    {
        await using var database = await _fixture.CreateDatabaseAsync();
        await using var connection = database.CreateConnection();
        await connection.OpenAsync();
        await using var otherConnection = database.CreateConnection();
        await otherConnection.OpenAsync();
        await using var otherTransaction = await otherConnection.BeginTransactionAsync();
        var factory = database.CreateSessionFactory();

        var exception = Assert.Throws<InvalidOperationException>(
            () => factory.CreateForTransaction(connection, otherTransaction));

        Assert.Contains("provided DbConnection", exception.Message, StringComparison.Ordinal);
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
        Assert.False(string.IsNullOrWhiteSpace(operation.OutgoingMessageId));

        await store.MarkDispatchedAsync(operation.Id);
        var dispatchedUtc = await database.QueryScalarAsync<DateTimeOffset?>(
            $"SELECT DispatchedUtc FROM {database.OutboxTableName} WHERE Id = @Id;",
            operation.Id);

        Assert.NotNull(dispatchedUtc);
    }

    [SqlServerFact]
    public async Task OutboxStore_ReusesDeterministicOutgoingMessageIdAcrossReplay()
    {
        await using var database = await _fixture.CreateDatabaseAsync();
        await using var session = await database.CreateSessionAsync();

        await session.CommitAsync(
            database.CreateInboxMessage("message-for-replay"),
            new[] { CreateOutboxOperation() });

        var store = database.CreateOutboxStore();
        var firstClaim = Assert.Single(await store.ClaimPendingAsync(batchSize: 10));

        await store.MarkFailedAsync(firstClaim.Id, new InvalidOperationException("transport failed"));

        var secondClaim = Assert.Single(await store.ClaimPendingAsync(batchSize: 10));

        Assert.Equal(firstClaim.Id, secondClaim.Id);
        Assert.Equal(firstClaim.OutgoingMessageId, secondClaim.OutgoingMessageId);
        Assert.Equal(2, secondClaim.AttemptCount);
    }

    [SqlServerFact]
    public async Task Commit_GeneratesDistinctDeterministicOutgoingMessageIds()
    {
        await using var database = await _fixture.CreateDatabaseAsync();
        await using var session = await database.CreateSessionAsync();

        await session.CommitAsync(
            database.CreateInboxMessage("message-multiple-outbox"),
            new[]
            {
                CreateOutboxOperation(),
                CreateOutboxOperation()
            });

        var outgoingMessageIds = await database.QueryStringsAsync(
            $"SELECT OutgoingMessageId FROM {database.OutboxTableName} ORDER BY CreatedUtc, Id;");

        Assert.Equal(2, outgoingMessageIds.Count);
        Assert.NotEqual(outgoingMessageIds[0], outgoingMessageIds[1]);
    }

    [SqlServerFact]
    public async Task OutboxStore_ReclaimsOperationAfterClaimLeaseExpires()
    {
        await using var database = await _fixture.CreateDatabaseAsync(options =>
        {
            options.OutboxClaimLeaseDuration = TimeSpan.FromSeconds(30);
        });
        await using var session = await database.CreateSessionAsync();

        await session.CommitAsync(
            database.CreateInboxMessage("message-claim-lease"),
            new[] { CreateOutboxOperation() });

        var store = database.CreateOutboxStore();
        var firstClaim = Assert.Single(await store.ClaimPendingAsync(batchSize: 10));

        Assert.Empty(await store.ClaimPendingAsync(batchSize: 10));

        await database.ExecuteNonQueryAsync(
            $"""
            UPDATE {database.OutboxTableName}
            SET ClaimedUtc = DATEADD(second, -31, SYSUTCDATETIME())
            WHERE Id = @Id;
            """,
            new SqlParameter("@Id", firstClaim.Id));

        var reclaimed = Assert.Single(await store.ClaimPendingAsync(batchSize: 10));
        Assert.Equal(firstClaim.Id, reclaimed.Id);
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
    public async Task HostedOutboxDispatcher_DispatchCycle_DrainsRealSqlOutboxStore()
    {
        await using var database = await _fixture.CreateDatabaseAsync(options =>
        {
            options.DispatcherBatchSize = 1;
        });
        await using var session = await database.CreateSessionAsync();
        await session.CommitAsync(
            database.CreateInboxMessage("message-hosted-dispatch"),
            new[]
            {
                CreateOutboxOperation(),
                CreateOutboxOperation()
            });
        var transportDispatcher = new RecordingOutboxDispatcher();
        using var serviceProvider = new ServiceCollection()
            .AddSingleton<IMessageSerializer, JsonMessageSerializer>()
            .AddSingleton<IMiniBusOutboxDispatcher>(transportDispatcher)
            .AddMiniBusSqlPersistence(options =>
            {
                options.SchemaName = database.SchemaName;
                options.ConnectionFactory = database.CreateConnection;
                options.DispatcherBatchSize = 1;
            })
            .AddMiniBusSqlHostedOutboxDispatch(options =>
            {
                options.PollInterval = TimeSpan.FromMilliseconds(10);
                options.MaxBatchesPerCycle = 5;
                options.FailureBackoff = TimeSpan.FromMilliseconds(10);
                options.DrainOnStartup = false;
            })
            .BuildServiceProvider();
        var hostedService = Assert.Single(
            serviceProvider.GetServices<IHostedService>(),
            service => service.GetType() == typeof(SqlMiniBusOutboxHostedDispatcher));

        try
        {
            await hostedService.StartAsync(CancellationToken.None);
            using var waitCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await WaitUntilAsync(
                async () => await database.CountDispatchedOutboxRowsAsync() == 2,
                "SQL hosted outbox dispatcher to dispatch the two pending outbox rows",
                waitCancellation.Token);
        }
        finally
        {
            using var stopCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await hostedService.StopAsync(stopCancellation.Token);
        }

        Assert.Equal(2, transportDispatcher.DispatchedCount);
        Assert.Equal(2, await database.CountDispatchedOutboxRowsAsync());
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

    [SqlServerFact]
    public async Task Cleanup_RemovesOnlyExpiredEligibleRowsWithinBatchLimit()
    {
        await using var database = await _fixture.CreateDatabaseAsync(options =>
        {
            options.InboxRetention = TimeSpan.FromDays(7);
            options.DispatchedOutboxRetention = TimeSpan.FromDays(3);
            options.FailedOutboxRetention = null;
            options.CleanupBatchSize = 1;
        });
        await using var oldSession = await database.CreateSessionAsync();
        await oldSession.CommitAsync(
            database.CreateInboxMessage("old-message"),
            new[] { CreateOutboxOperation() });
        await using var freshSession = await database.CreateSessionAsync();
        await freshSession.CommitAsync(
            database.CreateInboxMessage("fresh-message"),
            new[] { CreateOutboxOperation() });

        await database.ExecuteNonQueryAsync($"""
            UPDATE {database.InboxTableName}
            SET ProcessedUtc = DATEADD(day, -10, SYSUTCDATETIME())
            WHERE MessageId = @MessageId;
            """, new SqlParameter("@MessageId", "old-message"));
        await database.ExecuteNonQueryAsync($"""
            UPDATE {database.OutboxTableName}
            SET DispatchedUtc = DATEADD(day, -4, SYSUTCDATETIME())
            WHERE IncomingMessageId = @MessageId;
            """, new SqlParameter("@MessageId", "old-message"));
        await database.ExecuteNonQueryAsync($"""
            UPDATE {database.OutboxTableName}
            SET LastError = N'failed', CreatedUtc = DATEADD(day, -30, SYSUTCDATETIME())
            WHERE IncomingMessageId = @MessageId;
            """, new SqlParameter("@MessageId", "fresh-message"));

        var store = database.CreateOutboxStore();
        var deleted = await store.CleanupAsync();

        Assert.Equal(2, deleted);
        Assert.Equal(1, await database.CountRowsAsync(database.InboxTableName));
        Assert.Equal(1, await database.CountRowsAsync(database.OutboxTableName));
    }

    [SqlServerFact]
    public async Task SagaPersistence_CreatesLoadsSavesAndCompletesSagaData()
    {
        await using var database = await _fixture.CreateDatabaseAsync();
        var persistence = database.CreateSagaPersistence();
        var data = CreateSagaData("saga-lifecycle");

        await persistence.CreateAsync(data);

        var loaded = await persistence.LoadAsync<TestSagaData>("saga-lifecycle");
        Assert.NotNull(loaded);
        Assert.False(string.IsNullOrWhiteSpace(loaded.Version));
        Assert.Equal(data.Id, loaded.Data.Id);
        Assert.Equal("saga-lifecycle", loaded.Data.CorrelationId);
        Assert.Equal("started", loaded.Data.Status);

        loaded.Data.Status = "updated";
        await persistence.SaveAsync(loaded.Data, loaded.Version);

        var saved = await persistence.LoadAsync<TestSagaData>("saga-lifecycle");
        Assert.NotNull(saved);
        Assert.Equal("updated", saved.Data.Status);
        Assert.NotEqual(loaded.Version, saved.Version);

        await persistence.CompleteAsync(saved.Data, saved.Version);

        var completed = await persistence.LoadAsync<TestSagaData>("saga-lifecycle");
        Assert.NotNull(completed);
        Assert.True(completed.Data.IsCompleted);
        Assert.Equal(1, await database.CountRowsAsync(database.SagaTableName));
        Assert.NotNull(await database.QueryScalarAsync<DateTimeOffset?>(
            $"SELECT CompletedUtc FROM {database.SagaTableName} WHERE Id = @Id;",
            data.Id));
    }

    [SqlServerFact]
    public async Task SagaPersistence_RejectsDuplicateMissingAndStaleState()
    {
        await using var database = await _fixture.CreateDatabaseAsync();
        var persistence = database.CreateSagaPersistence();
        var data = CreateSagaData("saga-concurrency");

        await persistence.CreateAsync(data);

        await Assert.ThrowsAsync<SagaPersistenceException>(() =>
            persistence.CreateAsync(CreateSagaData("saga-concurrency")));

        await Assert.ThrowsAsync<SagaPersistenceException>(() =>
            persistence.SaveAsync(CreateSagaData("missing-saga"), version: "AQIDBA=="));

        var firstLoad = await persistence.LoadAsync<TestSagaData>("saga-concurrency");
        var secondLoad = await persistence.LoadAsync<TestSagaData>("saga-concurrency");
        Assert.NotNull(firstLoad);
        Assert.NotNull(secondLoad);

        firstLoad.Data.Status = "first";
        await persistence.SaveAsync(firstLoad.Data, firstLoad.Version);

        secondLoad.Data.Status = "second";
        var stale = await Assert.ThrowsAsync<SagaPersistenceException>(() =>
            persistence.SaveAsync(secondLoad.Data, secondLoad.Version));
        Assert.Contains("stale version", stale.Message, StringComparison.Ordinal);
    }

    [SqlServerFact]
    public async Task SagaPersistence_RoundTripsReferenceTypeProperties()
    {
        await using var database = await _fixture.CreateDatabaseAsync();
        var persistence = database.CreateSagaPersistence();
        var data = CreateSagaData("saga-serialization");
        data.Details = new TestSagaDetails
        {
            CustomerId = "customer-1",
            Attempts = new List<int> { 1, 2, 3 }
        };

        await persistence.CreateAsync(data);

        var loaded = await persistence.LoadAsync<TestSagaData>("saga-serialization");

        Assert.NotNull(loaded);
        Assert.NotSame(data, loaded.Data);
        Assert.NotNull(loaded.Data.Details);
        Assert.Equal("customer-1", loaded.Data.Details.CustomerId);
        Assert.Equal(new[] { 1, 2, 3 }, loaded.Data.Details.Attempts);
    }

    [SqlServerFact]
    public async Task SagaTimeout_SuccessPersistsSagaStateAndOutboxSchedule()
    {
        await using var database = await _fixture.CreateDatabaseAsync();
        var persistence = database.CreateSagaPersistence();
        var registry = new SagaRegistry();
        registry.Register<TimeoutRequestSaga, TestSagaData>();
        var invoker = new SagaInvoker(registry, persistence);
        var dueTime = new DateTimeOffset(2026, 5, 15, 12, 0, 0, TimeSpan.Zero);
        var context = new RecordingMiniBusContext();

        await invoker.InvokeAsync(
            new StartTimeoutSaga("saga-timeout", dueTime),
            context,
            EmptyServiceProvider.Instance);

        await using var session = await database.CreateSessionAsync();
        await session.CommitAsync(
            database.CreateInboxMessage("message-timeout"),
            context.Operations);

        var saga = await persistence.LoadAsync<TestSagaData>("saga-timeout");
        Assert.NotNull(saga);
        Assert.Equal("timeout-requested", saga.Data.Status);
        Assert.Equal(1, await database.CountRowsAsync(database.SagaTableName));

        await using var connection = database.CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT OperationKind, MessageType, HeadersJson, DueTime
            FROM {database.OutboxTableName};
            """;

        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(MiniBusOutboxOperationKind.Schedule.ToString(), reader.GetString(0));
        Assert.Contains(nameof(TestTimeout), reader.GetString(1), StringComparison.Ordinal);
        Assert.Contains("correlation-1", reader.GetString(2), StringComparison.Ordinal);
        Assert.Contains("message-1", reader.GetString(2), StringComparison.Ordinal);
        Assert.Equal(dueTime, reader.GetFieldValue<DateTimeOffset>(3));
        Assert.False(await reader.ReadAsync());
    }

    [SqlServerFact]
    public async Task SagaTimeout_FailedSagaDoesNotCommitOutboxSchedule()
    {
        await using var database = await _fixture.CreateDatabaseAsync();
        var persistence = database.CreateSagaPersistence();
        var registry = new SagaRegistry();
        registry.Register<FailingTimeoutRequestSaga, TestSagaData>();
        var invoker = new SagaInvoker(registry, persistence);
        var context = new RecordingMiniBusContext();

        await Assert.ThrowsAsync<InvalidOperationException>(() => invoker.InvokeAsync(
            new StartTimeoutSaga("saga-timeout-failure", DateTimeOffset.UtcNow.AddMinutes(5)),
            context,
            EmptyServiceProvider.Instance));

        Assert.Single(context.Operations);
        Assert.Equal(0, await database.CountRowsAsync(database.SagaTableName));
        Assert.Equal(0, await database.CountRowsAsync(database.OutboxTableName));
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

    private static TestSagaData CreateSagaData(string correlationId)
    {
        return new TestSagaData
        {
            Id = Guid.NewGuid(),
            CorrelationId = correlationId,
            Status = "started"
        };
    }

    private static async Task WaitUntilAsync(
        Func<Task<bool>> condition,
        string description,
        CancellationToken cancellationToken,
        TimeSpan? pollInterval = null)
    {
        var delay = pollInterval ?? TimeSpan.FromMilliseconds(25);

        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (await condition())
                {
                    return;
                }

                await Task.Delay(delay, cancellationToken);
            }
        }
        catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested)
        {
            throw new InvalidOperationException(
                $"Timed out waiting for {description}.",
                exception);
        }
    }

    private sealed record TestCommand(Guid Id) : ICommand;

    private sealed class RecordingOutboxDispatcher : IMiniBusOutboxDispatcher
    {
        private readonly ConcurrentQueue<MiniBusOutboxStoredOperation> _dispatched = new();

        public int DispatchedCount => _dispatched.Count;

        public IReadOnlyList<MiniBusOutboxStoredOperation> DispatchedSnapshot => _dispatched.ToArray();

        public Task DispatchAsync(
            MiniBusOutboxStoredOperation operation,
            CancellationToken cancellationToken = default)
        {
            _dispatched.Enqueue(operation);
            return Task.CompletedTask;
        }
    }

    private sealed record StartTimeoutSaga(string CorrelationId, DateTimeOffset DueTime) : ICommand;

    private sealed record TestTimeout(string CorrelationId) : ISagaTimeout;

    public sealed class TestSagaData : ISagaData
    {
        public Guid Id { get; set; }

        public string CorrelationId { get; set; } = string.Empty;

        public bool IsCompleted { get; set; }

        public string Status { get; set; } = string.Empty;

        public TestSagaDetails? Details { get; set; }
    }

    public sealed class TestSagaDetails
    {
        public string CustomerId { get; set; } = string.Empty;

        public List<int> Attempts { get; set; } = new();
    }

    private sealed class TimeoutRequestSaga :
        MiniBusSaga<TestSagaData>,
        IHandleSagaMessages<StartTimeoutSaga>,
        IHandleSagaMessages<TestTimeout>
    {
        public override void ConfigureHowToFindSaga(SagaMapper<TestSagaData> mapper)
        {
            mapper.StartsWith<StartTimeoutSaga>(message => message.CorrelationId)
                .Correlate<TestTimeout>(message => message.CorrelationId);
        }

        public async Task Handle(StartTimeoutSaga message, MiniBusContext context, CancellationToken cancellationToken)
        {
            Data.Status = "timeout-requested";
            await RequestTimeout(
                    new TestTimeout(message.CorrelationId),
                    message.DueTime,
                    context,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        public Task Handle(TestTimeout message, MiniBusContext context, CancellationToken cancellationToken)
        {
            Data.Status = "timed-out";
            return Task.CompletedTask;
        }
    }

    private sealed class FailingTimeoutRequestSaga :
        MiniBusSaga<TestSagaData>,
        IHandleSagaMessages<StartTimeoutSaga>
    {
        public override void ConfigureHowToFindSaga(SagaMapper<TestSagaData> mapper)
        {
            mapper.StartsWith<StartTimeoutSaga>(message => message.CorrelationId);
        }

        public async Task Handle(StartTimeoutSaga message, MiniBusContext context, CancellationToken cancellationToken)
        {
            await RequestTimeout(
                    new TestTimeout(message.CorrelationId),
                    message.DueTime,
                    context,
                    cancellationToken)
                .ConfigureAwait(false);

            throw new InvalidOperationException("saga failed");
        }
    }

    private sealed class RecordingMiniBusContext : MiniBusContext
    {
        public List<MiniBusOutboxOperation> Operations { get; } = new();

        public override string EndpointName => "Billing";

        public override string MessageId => "message-1";

        public override string CorrelationId => "correlation-1";

        public override string? CausationId => null;

        public override IReadOnlyDictionary<string, string> Headers { get; } = new Dictionary<string, string>
        {
            [MiniBusHeaderNames.CorrelationId] = "correlation-1"
        };

        public override Task Send<TCommand>(TCommand command, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public override Task Publish<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public override Task Schedule<TMessage>(
            TMessage message,
            DateTimeOffset dueTime,
            CancellationToken cancellationToken = default)
        {
            Operations.Add(new MiniBusOutboxOperation(
                MiniBusOutboxOperationKind.Schedule,
                message!,
                typeof(TMessage),
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [MiniBusHeaderNames.CorrelationId] = CorrelationId,
                    [MiniBusHeaderNames.CausationId] = MessageId
                },
                dueTime));

            return Task.CompletedTask;
        }
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public static EmptyServiceProvider Instance { get; } = new();

        public object? GetService(Type serviceType)
        {
            return null;
        }
    }

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

        internal async Task<SqlServerTestDatabase> CreateDatabaseAsync(
            Action<MiniBusSqlPersistenceOptions>? configureOptions = null)
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
                $"MiniBusTest_{Guid.NewGuid():N}",
                configureOptions);
            await database.ApplySchemaAsync();
            return database;
        }
    }

    internal sealed class SqlServerTestDatabase : IAsyncDisposable
    {
        private readonly MiniBusSqlPersistenceOptions _options;
        private readonly SqlOutboxOperationSerializer _operationSerializer;

        internal SqlServerTestDatabase(
            string connectionString,
            string schemaName,
            Action<MiniBusSqlPersistenceOptions>? configureOptions)
        {
            ConnectionString = connectionString;
            SchemaName = schemaName;
            InboxTableName = $"[{SchemaName}].[Inbox]";
            OutboxTableName = $"[{SchemaName}].[Outbox]";
            SagaTableName = $"[{SchemaName}].[Sagas]";
            BusinessTableName = $"[{SchemaName}].[BusinessData]";
            _options = new MiniBusSqlPersistenceOptions
            {
                SchemaName = SchemaName,
                ConnectionFactory = CreateConnection
            };
            configureOptions?.Invoke(_options);
            _operationSerializer = new SqlOutboxOperationSerializer(new JsonMessageSerializer());
        }

        public string ConnectionString { get; }

        public string SchemaName { get; }

        public string InboxTableName { get; }

        public string OutboxTableName { get; }

        public string SagaTableName { get; }

        public string BusinessTableName { get; }

        public SqlConnection CreateConnection()
        {
            return new SqlConnection(ConnectionString);
        }

        public async ValueTask<IMiniBusPersistenceSession> CreateSessionAsync()
        {
            return await CreateSessionFactory().CreateAsync();
        }

        public SqlMiniBusPersistenceSessionFactory CreateSessionFactory()
        {
            return new SqlMiniBusPersistenceSessionFactory(_options, _operationSerializer);
        }

        public ISqlMiniBusOutboxStore CreateOutboxStore()
        {
            return new SqlMiniBusOutboxStore(_options, _operationSerializer);
        }

        public SqlMiniBusOutboxDispatcher CreateOutboxDispatcher(IMiniBusOutboxDispatcher dispatcher)
        {
            return new SqlMiniBusOutboxDispatcher(
                CreateOutboxStore(),
                dispatcher,
                _options);
        }

        public ISagaPersistence CreateSagaPersistence()
        {
            return new SqlSagaPersistence(
                _options,
                new SqlSagaDataSerializer(new JsonMessageSerializer()));
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

        public async Task<IReadOnlyList<string>> QueryStringsAsync(string commandText)
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = commandText;

            var values = new List<string>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                values.Add(reader.GetString(0));
            }

            return values;
        }

        public async Task<int> CountRowsAsync(string tableName)
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"SELECT COUNT(*) FROM {tableName};";
            return Convert.ToInt32(await command.ExecuteScalarAsync());
        }

        public async Task<int> CountDispatchedOutboxRowsAsync()
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"""
                SELECT COUNT(*)
                FROM {OutboxTableName}
                WHERE DispatchedUtc IS NOT NULL;
                """;
            return Convert.ToInt32(await command.ExecuteScalarAsync());
        }

        public async Task CreateBusinessTableAsync()
        {
            await ExecuteNonQueryAsync($"""
                CREATE TABLE {BusinessTableName}
                (
                    Id int NOT NULL CONSTRAINT PK_{SchemaName}_BusinessData PRIMARY KEY
                );
                """);
        }

        public async Task InsertBusinessRecordAsync(
            DbConnection connection,
            DbTransaction transaction,
            int id)
        {
            await using DbCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = $"INSERT INTO {BusinessTableName} (Id) VALUES (@Id);";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@Id";
            parameter.Value = id;
            command.Parameters.Add(parameter);
            await command.ExecuteNonQueryAsync();
        }

        public async Task DropOutboxTableAsync()
        {
            await ExecuteNonQueryAsync($"DROP TABLE {OutboxTableName};");
        }

        public async ValueTask DisposeAsync()
        {
            await ExecuteNonQueryAsync($"""
                IF OBJECT_ID(N'{SchemaName}.BusinessData', N'U') IS NOT NULL
                    DROP TABLE {BusinessTableName};

                IF OBJECT_ID(N'{SchemaName}.Outbox', N'U') IS NOT NULL
                    DROP TABLE {OutboxTableName};

                IF OBJECT_ID(N'{SchemaName}.Sagas', N'U') IS NOT NULL
                    DROP TABLE {SagaTableName};

                IF OBJECT_ID(N'{SchemaName}.Inbox', N'U') IS NOT NULL
                    DROP TABLE {InboxTableName};

                IF SCHEMA_ID(N'{SchemaName}') IS NOT NULL
                    EXEC(N'DROP SCHEMA [{SchemaName}]');
                """);
        }

        public async Task ApplySchemaAsync()
        {
            var schemaDirectory = Path.GetFullPath(
                "../../../../../src/MiniBus.Persistence.Sql/Schema",
                AppContext.BaseDirectory);
            var scriptPaths = Directory.GetFiles(schemaDirectory, "*.sql");
            var invalidScript = scriptPaths
                .Select(Path.GetFileName)
                .FirstOrDefault(name => !IsVersionedSchemaScriptName(name));

            if (invalidScript is not null)
            {
                throw new InvalidOperationException(
                    $"MiniBus SQL schema scripts must use a three-digit migration prefix such as '001-'. Invalid script: {invalidScript}");
            }

            foreach (var scriptPath in scriptPaths.Order(StringComparer.Ordinal))
            {
                var script = File.ReadAllText(scriptPath);
                script = script
                    .Replace("SCHEMA_ID(N'MiniBus')", $"SCHEMA_ID(N'{SchemaName}')", StringComparison.Ordinal)
                    .Replace("CREATE SCHEMA MiniBus", $"CREATE SCHEMA [{SchemaName}]", StringComparison.Ordinal)
                    .Replace("OBJECT_ID(N'MiniBus.Outbox'", $"OBJECT_ID(N'{SchemaName}.Outbox'", StringComparison.Ordinal)
                    .Replace("OBJECT_ID(N'MiniBus.Inbox'", $"OBJECT_ID(N'{SchemaName}.Inbox'", StringComparison.Ordinal)
                    .Replace("N'MiniBus.Outbox'", $"N'{SchemaName}.Outbox'", StringComparison.Ordinal)
                    .Replace("N'MiniBus'", $"N'{SchemaName}'", StringComparison.Ordinal)
                    .Replace("MiniBus.", $"{SchemaName}.", StringComparison.Ordinal);

                await ExecuteNonQueryAsync(script);
            }
        }

        private static bool IsVersionedSchemaScriptName(string? fileName)
        {
            return fileName is { Length: > 4 }
                   && char.IsDigit(fileName[0])
                   && char.IsDigit(fileName[1])
                   && char.IsDigit(fileName[2])
                   && fileName[3] == '-';
        }

        public async Task ExecuteNonQueryAsync(string commandText, params SqlParameter[] parameters)
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = commandText;
            command.Parameters.AddRange(parameters);
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
            using var stream = body.ToStream();
            return JsonSerializer.Deserialize(stream, messageType)
                   ?? throw new InvalidOperationException($"Failed to deserialize body as {messageType.FullName}.");
        }
    }
}
