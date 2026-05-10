using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using MiniBus.Core.Contracts;
using MiniBus.Core.Headers;
using MiniBus.Core.Persistence;
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
                    options.DispatcherBatchSize = 17;
                })
            .BuildServiceProvider();

        var options = serviceProvider.GetRequiredService<MiniBusSqlPersistenceOptions>();

        Assert.Equal("CustomSchema", options.SchemaName);
        Assert.Equal("CustomInbox", options.InboxTableName);
        Assert.Equal("CustomOutbox", options.OutboxTableName);
        Assert.Equal(17, options.DispatcherBatchSize);
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
            serialized.OperationKind,
            serialized.MessageType,
            serialized.Body,
            serialized.HeadersJson,
            serialized.DueTime,
            attemptCount: 3);

        Assert.Equal(MiniBusOutboxOperationKind.Schedule, stored.Kind);
        Assert.Equal(typeof(TestCommand), stored.MessageType);
        Assert.Equal("serialized:TestCommand", stored.Body.ToString());
        Assert.Equal("correlation-1", stored.Headers[MiniBusHeaderNames.CorrelationId]);
        Assert.Equal("message-1", stored.Headers[MiniBusHeaderNames.CausationId]);
        Assert.Equal(dueTime, stored.DueTime);
        Assert.Equal(3, stored.AttemptCount);
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
        Assert.Contains("HeadersJson", script, StringComparison.Ordinal);
        Assert.Contains("AttemptCount", script, StringComparison.Ordinal);
        Assert.Contains("DispatchedUtc", script, StringComparison.Ordinal);
    }

    private static MiniBusOutboxStoredOperation CreateStoredOperation()
    {
        return new MiniBusOutboxStoredOperation(
            Guid.NewGuid(),
            MiniBusOutboxOperationKind.Send,
            BinaryData.FromString("{}"),
            typeof(TestCommand),
            new Dictionary<string, string>(StringComparer.Ordinal),
            DueTime: null,
            AttemptCount: 0);
    }

    private sealed record TestCommand(Guid Id) : ICommand;

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
