using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using MiniBus.AzureFunctions.DependencyInjection;
using MiniBus.Core.ClaimCheck;
using MiniBus.Core.Contracts;
using MiniBus.Core.Headers;
using MiniBus.Core.Persistence;
using MiniBus.Core.Sagas;
using MiniBus.Core.Serialization;
using MiniBus.Persistence.Sql.DependencyInjection;
using Xunit;

namespace MiniBus.Persistence.Sql.Tests;

public sealed class SqlPersistenceTests
{
    [Fact]
    public void AddMiniBusSqlPersistence_WithConnectionString_ConfiguresSqlConnectionFactory()
    {
        using var serviceProvider = new ServiceCollection()
            .AddSingleton<IMessageSerializer, RecordingSerializer>()
            .AddMiniBusSqlPersistence(
                "Server=(localdb)\\MSSQLLocalDB;Database=MiniBusTests;Integrated Security=true;Encrypt=false",
                options =>
                {
                    options.SchemaName = "CustomSchema";
                    options.InboxTableName = "CustomInbox";
                    options.OutboxTableName = "CustomOutbox";
                    options.SagaTableName = "CustomSagas";
                    options.DispatcherBatchSize = 17;
                    options.OutboxClaimLeaseDuration = TimeSpan.FromMinutes(3);
                    options.InboxRetention = TimeSpan.FromDays(30);
                    options.DispatchedOutboxRetention = TimeSpan.FromDays(7);
                    options.FailedOutboxRetention = TimeSpan.FromDays(14);
                    options.CleanupBatchSize = 25;
                })
            .BuildServiceProvider();

        var options = serviceProvider.GetRequiredService<MiniBusSqlPersistenceOptions>();

        Assert.Equal("CustomSchema", options.SchemaName);
        Assert.Equal("CustomInbox", options.InboxTableName);
        Assert.Equal("CustomOutbox", options.OutboxTableName);
        Assert.Equal("CustomSagas", options.SagaTableName);
        Assert.Equal(17, options.DispatcherBatchSize);
        Assert.Equal(TimeSpan.FromMinutes(3), options.OutboxClaimLeaseDuration);
        Assert.Equal(TimeSpan.FromDays(30), options.InboxRetention);
        Assert.Equal(TimeSpan.FromDays(7), options.DispatchedOutboxRetention);
        Assert.Equal(TimeSpan.FromDays(14), options.FailedOutboxRetention);
        Assert.Equal(25, options.CleanupBatchSize);
        Assert.NotNull(options.ConnectionFactory);

        using var connection = Assert.IsType<SqlConnection>(options.ConnectionFactory());
        Assert.Contains("MiniBusTests", connection.ConnectionString, StringComparison.Ordinal);
    }

    [Fact]
    public void AddMiniBusSqlPersistence_UsesExplicitConnectionFactoryWhenConnectionStringIsAlsoConfigured()
    {
        using var serviceProvider = new ServiceCollection()
            .AddSingleton<IMessageSerializer, RecordingSerializer>()
            .AddMiniBusSqlPersistence(
                "Server=from-connection-string;Database=MiniBusTests;Encrypt=false",
                options =>
                {
                    options.ConnectionFactory = () => new SqlConnection(
                        "Server=from-factory;Database=MiniBusTests;Encrypt=false");
                })
            .BuildServiceProvider();

        var options = serviceProvider.GetRequiredService<MiniBusSqlPersistenceOptions>();

        Assert.NotNull(options.ConnectionFactory);
        using var connection = Assert.IsType<SqlConnection>(options.ConnectionFactory());
        Assert.Contains("from-factory", connection.ConnectionString, StringComparison.Ordinal);
    }

    [Fact]
    public void AddMiniBusSqlPersistence_WithOptionsConnectionString_ConfiguresSqlConnectionFactory()
    {
        using var serviceProvider = new ServiceCollection()
            .AddSingleton<IMessageSerializer, RecordingSerializer>()
            .AddMiniBusSqlPersistence(options =>
            {
                options.ConnectionString = "Server=from-options;Database=MiniBusTests;Encrypt=false";
            })
            .BuildServiceProvider();

        var options = serviceProvider.GetRequiredService<MiniBusSqlPersistenceOptions>();

        Assert.NotNull(options.ConnectionFactory);
        using var connection = Assert.IsType<SqlConnection>(options.ConnectionFactory());
        Assert.Contains("from-options", connection.ConnectionString, StringComparison.Ordinal);
    }

    [Fact]
    public void AddMiniBusSqlPersistence_RegistersSqlSagaPersistence()
    {
        using var serviceProvider = new ServiceCollection()
            .AddSingleton<IMessageSerializer, RecordingSerializer>()
            .AddMiniBusSqlPersistence(
                "Server=(localdb)\\MSSQLLocalDB;Database=MiniBusTests;Integrated Security=true;Encrypt=false")
            .BuildServiceProvider();

        Assert.IsType<SqlSagaPersistence>(serviceProvider.GetRequiredService<ISagaPersistence>());
    }

    [Fact]
    public void AddMiniBusSqlPersistence_AfterAzureFunctions_OverridesFallbackSagaPersistence()
    {
        using var serviceProvider = new ServiceCollection()
            .AddSingleton<IMessageSerializer, RecordingSerializer>()
            .AddMiniBusAzureFunctions()
            .AddMiniBusSqlPersistence(
                "Server=(localdb)\\MSSQLLocalDB;Database=MiniBusTests;Integrated Security=true;Encrypt=false")
            .BuildServiceProvider();

        Assert.IsType<SqlSagaPersistence>(serviceProvider.GetRequiredService<ISagaPersistence>());
    }

    [Fact]
    public void AddMiniBusSqlPersistence_BeforeAzureFunctions_IsNotOverriddenByFallbackSagaPersistence()
    {
        using var serviceProvider = new ServiceCollection()
            .AddSingleton<IMessageSerializer, RecordingSerializer>()
            .AddMiniBusSqlPersistence(
                "Server=(localdb)\\MSSQLLocalDB;Database=MiniBusTests;Integrated Security=true;Encrypt=false")
            .AddMiniBusAzureFunctions()
            .BuildServiceProvider();

        Assert.IsType<SqlSagaPersistence>(serviceProvider.GetRequiredService<ISagaPersistence>());
    }

    [Fact]
    public void AddMiniBusSqlPersistence_DoesNotOverrideExistingCustomSagaPersistence()
    {
        using var serviceProvider = new ServiceCollection()
            .AddSingleton<IMessageSerializer, RecordingSerializer>()
            .AddSingleton<ISagaPersistence, CustomSagaPersistence>()
            .AddMiniBusSqlPersistence(
                "Server=(localdb)\\MSSQLLocalDB;Database=MiniBusTests;Integrated Security=true;Encrypt=false")
            .BuildServiceProvider();

        Assert.IsType<CustomSagaPersistence>(serviceProvider.GetRequiredService<ISagaPersistence>());
    }

    [Fact]
    public void OutboxOperationSerializer_PreservesMessageBodyMetadataHeadersAndDueTime()
    {
        var serializer = new SqlOutboxOperationSerializer(new RecordingSerializer());
        var dueTime = DateTimeOffset.UtcNow.AddMinutes(5);
        var operation = new MiniBusOutboxOperation(
            MiniBusOutboxOperationKind.Schedule,
            new TestCommand(Guid.NewGuid()),
            typeof(TestCommand),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [MiniBusHeaderNames.CorrelationId] = "correlation-1",
                [MiniBusHeaderNames.CausationId] = "message-1"
            },
            dueTime);

        var serialized = serializer.Serialize(operation);
        var stored = serializer.Deserialize(
            Guid.NewGuid(),
            "outgoing-message-1",
            serialized.OperationKind,
            serialized.MessageType,
            serialized.Body,
            serialized.HeadersJson,
            serialized.DueTime,
            attemptCount: 3);

        Assert.Equal(MiniBusOutboxOperationKind.Schedule, stored.Kind);
        Assert.Equal("outgoing-message-1", stored.OutgoingMessageId);
        Assert.Equal(typeof(TestCommand), stored.MessageType);
        Assert.Equal("serialized:TestCommand", stored.Body.ToString());
        Assert.Equal("correlation-1", stored.Headers[MiniBusHeaderNames.CorrelationId]);
        Assert.Equal("message-1", stored.Headers[MiniBusHeaderNames.CausationId]);
        Assert.Equal(dueTime, stored.DueTime);
        Assert.Equal(3, stored.AttemptCount);
    }

    [Fact]
    public void OutboxOperationSerializer_PreservesSagaTimeoutSchedule()
    {
        var serializer = new SqlOutboxOperationSerializer(new RecordingSerializer());
        var dueTime = DateTimeOffset.UtcNow.AddMinutes(5);
        var operation = new MiniBusOutboxOperation(
            MiniBusOutboxOperationKind.Schedule,
            new TestTimeout("saga-1"),
            typeof(TestTimeout),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [MiniBusHeaderNames.CorrelationId] = "correlation-1",
                [MiniBusHeaderNames.CausationId] = "message-1"
            },
            dueTime);

        var serialized = serializer.Serialize(operation);
        var stored = serializer.Deserialize(
            Guid.NewGuid(),
            "outgoing-timeout-1",
            serialized.OperationKind,
            serialized.MessageType,
            serialized.Body,
            serialized.HeadersJson,
            serialized.DueTime,
            attemptCount: 1);

        Assert.Equal(MiniBusOutboxOperationKind.Schedule, stored.Kind);
        Assert.Equal(typeof(TestTimeout), stored.MessageType);
        Assert.Equal("serialized:TestTimeout", stored.Body.ToString());
        Assert.Equal("correlation-1", stored.Headers[MiniBusHeaderNames.CorrelationId]);
        Assert.Equal("message-1", stored.Headers[MiniBusHeaderNames.CausationId]);
        Assert.Equal(dueTime, stored.DueTime);
    }

    [Fact]
    public async Task OutboxOperationSerializer_StoresClaimCheckBodyAndHeadersForLargePayload()
    {
        var store = new RecordingClaimCheckPayloadStore();
        var serializer = new SqlOutboxOperationSerializer(
            new RecordingSerializer(),
            new MiniBusClaimCheckOptions { Enabled = true, PayloadThresholdBytes = 3 },
            store);
        var operation = new MiniBusOutboxOperation(
            MiniBusOutboxOperationKind.Send,
            new TestCommand(Guid.NewGuid()),
            typeof(TestCommand),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [MiniBusHeaderNames.CorrelationId] = "correlation-1",
                [MiniBusHeaderNames.CausationId] = "message-1"
            },
            DueTime: null);

        var serialized = await serializer.SerializeAsync(operation);
        var stored = serializer.Deserialize(
            Guid.NewGuid(),
            "outgoing-message-1",
            serialized.OperationKind,
            serialized.MessageType,
            serialized.Body,
            serialized.HeadersJson,
            serialized.DueTime,
            attemptCount: 1);

        Assert.Equal("serialized:TestCommand", Assert.Single(store.Writes).Payload.ToString());
        Assert.NotEqual("serialized:TestCommand", stored.Body.ToString());
        Assert.Equal(bool.TrueString, stored.Headers[MiniBusClaimCheckHeaderNames.Enabled]);
        Assert.Equal(MiniBusClaimCheckProviderNames.AzureBlobStorage, stored.Headers[MiniBusClaimCheckHeaderNames.Provider]);
        Assert.Equal("payloads/payload-1.bin", stored.Headers[MiniBusClaimCheckHeaderNames.BlobName]);
        Assert.Equal("correlation-1", stored.Headers[MiniBusHeaderNames.CorrelationId]);
        Assert.Equal("message-1", stored.Headers[MiniBusHeaderNames.CausationId]);
    }

    [Fact]
    public void OutboxOperationSerializer_SyncSerializeRejectsEnabledClaimCheck()
    {
        var serializer = new SqlOutboxOperationSerializer(
            new RecordingSerializer(),
            new MiniBusClaimCheckOptions { Enabled = true, PayloadThresholdBytes = 3 },
            new RecordingClaimCheckPayloadStore());
        var operation = new MiniBusOutboxOperation(
            MiniBusOutboxOperationKind.Send,
            new TestCommand(Guid.NewGuid()),
            typeof(TestCommand),
            new Dictionary<string, string>(StringComparer.Ordinal),
            DueTime: null);

        var exception = Assert.Throws<InvalidOperationException>(() => serializer.Serialize(operation));

        Assert.Contains(nameof(SqlOutboxOperationSerializer.SerializeAsync), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OutboxDispatcher_MarksSuccessfulDispatch()
    {
        var operation = CreateStoredOperation();
        var store = new RecordingOutboxStore(operation);
        var dispatcher = new RecordingDispatcher();
        var sqlDispatcher = new SqlMiniBusOutboxDispatcher(
            store,
            dispatcher,
            new MiniBusSqlPersistenceOptions { DispatcherBatchSize = 10 });

        var dispatched = await sqlDispatcher.DispatchPendingAsync();

        Assert.Equal(1, dispatched);
        Assert.Equal(10, store.ClaimedBatchSize);
        Assert.Single(dispatcher.Dispatched);
        Assert.Contains(operation.Id, store.MarkedDispatched);
        Assert.Empty(store.MarkedFailed);
    }

    [Fact]
    public async Task OutboxDispatcher_RecordsFailureAndLeavesOperationRetryable()
    {
        var operation = CreateStoredOperation();
        var store = new RecordingOutboxStore(operation);
        var dispatcher = new RecordingDispatcher
        {
            Exception = new InvalidOperationException("transport failed")
        };
        var sqlDispatcher = new SqlMiniBusOutboxDispatcher(
            store,
            dispatcher,
            new MiniBusSqlPersistenceOptions { DispatcherBatchSize = 5 });

        var dispatched = await sqlDispatcher.DispatchPendingAsync();

        Assert.Equal(0, dispatched);
        Assert.Equal(5, store.ClaimedBatchSize);
        var failure = Assert.Single(store.MarkedFailed);
        Assert.Equal(operation.Id, failure.OperationId);
        Assert.Contains("transport failed", failure.Exception.Message, StringComparison.Ordinal);
        Assert.Empty(store.MarkedDispatched);
    }

    [Fact]
    public void SchemaScript_DefinesInboxAndOutboxOperationalColumns()
    {
        var script = File.ReadAllText(Path.GetFullPath(
            "../../../../../src/MiniBus.Persistence.Sql/Schema/001-inbox-outbox.sql",
            AppContext.BaseDirectory));

        Assert.Contains("CREATE TABLE MiniBus.Inbox", script, StringComparison.Ordinal);
        Assert.Contains("EndpointName", script, StringComparison.Ordinal);
        Assert.Contains("MessageId", script, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE MiniBus.Outbox", script, StringComparison.Ordinal);
        Assert.Contains("OperationKind", script, StringComparison.Ordinal);
        Assert.Contains("OutgoingMessageId", script, StringComparison.Ordinal);
        Assert.Contains("HeadersJson", script, StringComparison.Ordinal);
        Assert.Contains("AttemptCount", script, StringComparison.Ordinal);
        Assert.Contains("DispatchedUtc", script, StringComparison.Ordinal);
    }

    [Fact]
    public void AdditiveSchemaScript_AddsOutgoingMessageIdForExistingOutboxTables()
    {
        var script = File.ReadAllText(Path.GetFullPath(
            "../../../../../src/MiniBus.Persistence.Sql/Schema/002-outbox-outgoing-message-id.sql",
            AppContext.BaseDirectory));

        Assert.Contains("COL_LENGTH", script, StringComparison.Ordinal);
        Assert.Contains("OutgoingMessageId", script, StringComparison.Ordinal);
        Assert.Contains("UX_MiniBus_Outbox_OutgoingMessageId", script, StringComparison.Ordinal);
    }

    [Fact]
    public void SagaSchemaScript_DefinesSagaOperationalColumns()
    {
        var script = File.ReadAllText(Path.GetFullPath(
            "../../../../../src/MiniBus.Persistence.Sql/Schema/003-sagas.sql",
            AppContext.BaseDirectory));

        Assert.Contains("CREATE TABLE MiniBus.Sagas", script, StringComparison.Ordinal);
        Assert.Contains("DataType", script, StringComparison.Ordinal);
        Assert.Contains("CorrelationId", script, StringComparison.Ordinal);
        Assert.Contains("Data varbinary(max)", script, StringComparison.Ordinal);
        Assert.Contains("IsCompleted", script, StringComparison.Ordinal);
        Assert.Contains("CompletedUtc", script, StringComparison.Ordinal);
        Assert.Contains("Version rowversion", script, StringComparison.Ordinal);
        Assert.Contains("UX_MiniBus_Sagas_DataType_CorrelationId", script, StringComparison.Ordinal);
    }

    private static MiniBusOutboxStoredOperation CreateStoredOperation()
    {
        return new MiniBusOutboxStoredOperation(
            Guid.NewGuid(),
            "outgoing-message-1",
            MiniBusOutboxOperationKind.Send,
            BinaryData.FromString("{}"),
            typeof(TestCommand),
            new Dictionary<string, string>(StringComparer.Ordinal),
            DueTime: null,
            AttemptCount: 0);
    }

    private sealed record TestCommand(Guid Id) : ICommand;

    private sealed record TestTimeout(string CorrelationId) : ISagaTimeout;

    private sealed class CustomSagaPersistence : ISagaPersistence
    {
        public Task<SagaPersistenceRecord<TData>?> LoadAsync<TData>(
            string correlationId,
            CancellationToken cancellationToken = default)
            where TData : class, ISagaData, new()
        {
            throw new NotSupportedException();
        }

        public Task CreateAsync<TData>(
            TData data,
            CancellationToken cancellationToken = default)
            where TData : class, ISagaData, new()
        {
            throw new NotSupportedException();
        }

        public Task SaveAsync<TData>(
            TData data,
            string? version,
            CancellationToken cancellationToken = default)
            where TData : class, ISagaData, new()
        {
            throw new NotSupportedException();
        }

        public Task CompleteAsync<TData>(
            TData data,
            string? version,
            CancellationToken cancellationToken = default)
            where TData : class, ISagaData, new()
        {
            throw new NotSupportedException();
        }
    }

    private sealed class RecordingSerializer : IMessageSerializer
    {
        public BinaryData Serialize(object message, Type messageType)
        {
            return BinaryData.FromString($"serialized:{messageType.Name}");
        }

        public object Deserialize(BinaryData body, Type messageType)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class RecordingOutboxStore : ISqlMiniBusOutboxStore
    {
        private readonly IReadOnlyList<MiniBusOutboxStoredOperation> _operations;

        public RecordingOutboxStore(params MiniBusOutboxStoredOperation[] operations)
        {
            _operations = operations;
        }

        public int? ClaimedBatchSize { get; private set; }

        public List<Guid> MarkedDispatched { get; } = new();

        public List<(Guid OperationId, Exception Exception)> MarkedFailed { get; } = new();

        public Task<IReadOnlyList<MiniBusOutboxStoredOperation>> ClaimPendingAsync(
            int batchSize,
            CancellationToken cancellationToken = default)
        {
            ClaimedBatchSize = batchSize;
            return Task.FromResult(_operations);
        }

        public Task MarkDispatchedAsync(Guid operationId, CancellationToken cancellationToken = default)
        {
            MarkedDispatched.Add(operationId);
            return Task.CompletedTask;
        }

        public Task MarkFailedAsync(
            Guid operationId,
            Exception exception,
            CancellationToken cancellationToken = default)
        {
            MarkedFailed.Add((operationId, exception));
            return Task.CompletedTask;
        }

        public Task<int> CleanupAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(0);
        }
    }

    private sealed class RecordingClaimCheckPayloadStore : IMiniBusClaimCheckPayloadStore
    {
        public List<(BinaryData Payload, MiniBusClaimCheckPayloadWriteOptions? Options)> Writes { get; } = new();

        public Task<MiniBusClaimCheckPayloadReference> WriteAsync(
            BinaryData payload,
            MiniBusClaimCheckPayloadWriteOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            Writes.Add((payload, options));
            return Task.FromResult(new MiniBusClaimCheckPayloadReference(
                MiniBusClaimCheckProviderNames.AzureBlobStorage,
                "minibus-payloads",
                "payloads/payload-1.bin",
                "payload-1",
                payload.ToArray().LongLength,
                options?.ContentType,
                new DateTimeOffset(2026, 5, 15, 12, 0, 0, TimeSpan.Zero),
                null));
        }

        public Task<BinaryData> ReadAsync(
            MiniBusClaimCheckPayloadReference reference,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class RecordingDispatcher : IMiniBusOutboxDispatcher
    {
        public Exception? Exception { get; init; }

        public List<MiniBusOutboxStoredOperation> Dispatched { get; } = new();

        public Task DispatchAsync(
            MiniBusOutboxStoredOperation operation,
            CancellationToken cancellationToken = default)
        {
            if (Exception is not null)
            {
                return Task.FromException(Exception);
            }

            Dispatched.Add(operation);
            return Task.CompletedTask;
        }
    }
}
