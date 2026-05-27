using MiniBus.Core.Persistence;
using MiniBus.Persistence.Sql;
using MiniBus.Tooling.Core;
using MiniBus.Tooling.Sql;
using Xunit;

namespace MiniBus.Tooling.Sql.Tests;

public sealed class SqlMiniBusOutboxDrainActionTests
{
    [Fact]
    public void Constructor_RejectsNullDispatcher()
    {
        Assert.Throws<ArgumentNullException>(() => new SqlMiniBusOutboxDrainAction(null!));
    }

    [Fact]
    public async Task DrainAsync_ReusesExistingSqlDispatcher()
    {
        var operation = new MiniBusOutboxStoredOperation(
            Guid.NewGuid(),
            "outgoing-1",
            MiniBusOutboxOperationKind.Send,
            new BinaryData(Array.Empty<byte>()),
            typeof(TestMessage),
            new Dictionary<string, string>(),
            DueTime: null,
            AttemptCount: 0);
        var store = new RecordingOutboxStore(operation);
        var transport = new RecordingOutboxDispatcher();
        var sqlDispatcher = new SqlMiniBusOutboxDispatcher(
            store,
            transport,
            new MiniBusSqlPersistenceOptions());
        var action = new SqlMiniBusOutboxDrainAction(sqlDispatcher);

        var result = await action.DrainAsync(new MiniBusOutboxDrainRequest(MaxBatches: 1));

        Assert.True(result.IsSupported);
        Assert.True(result.Succeeded);
        Assert.Equal(1, result.DispatchedCount);
        Assert.Equal(new[] { operation.Id }, transport.Dispatched);
        Assert.Equal(new[] { operation.Id }, store.MarkedDispatched);
    }

    [Fact]
    public async Task DrainAsync_RejectsUnboundedRequestsBeforeDispatch()
    {
        var store = new RecordingOutboxStore();
        var transport = new RecordingOutboxDispatcher();
        var sqlDispatcher = new SqlMiniBusOutboxDispatcher(
            store,
            transport,
            new MiniBusSqlPersistenceOptions());
        var action = new SqlMiniBusOutboxDrainAction(sqlDispatcher);

        var result = await action.DrainAsync(new MiniBusOutboxDrainRequest(MaxBatches: 0));

        Assert.True(result.IsSupported);
        Assert.False(result.Succeeded);
        Assert.Contains("MaxBatches", result.Error, StringComparison.Ordinal);
        Assert.Empty(store.ClaimedBatchSizes);
        Assert.Empty(transport.Dispatched);
    }

    [Fact]
    public async Task DrainAsync_ReturnsStableFailureMessageWithoutRawProviderDetails()
    {
        var store = new FailingOutboxStore(
            "Server=tcp://secret.example;Password=super-secret; Authorization=Bearer auth-token");
        var sqlDispatcher = new SqlMiniBusOutboxDispatcher(
            store,
            new RecordingOutboxDispatcher(),
            new MiniBusSqlPersistenceOptions());
        var action = new SqlMiniBusOutboxDrainAction(sqlDispatcher);

        var result = await action.DrainAsync(new MiniBusOutboxDrainRequest(MaxBatches: 1));

        Assert.True(result.IsSupported);
        Assert.False(result.Succeeded);
        Assert.Equal(0, result.DispatchedCount);
        Assert.Contains("SQL outbox drain failed", result.Error, StringComparison.Ordinal);
        Assert.Contains("SQL_OUTBOX_DRAIN_FAILED", result.Error, StringComparison.Ordinal);
        Assert.Contains(nameof(InvalidOperationException), result.Error, StringComparison.Ordinal);
        Assert.Contains("Safe summary", result.Error, StringComparison.Ordinal);
        Assert.Contains("Server=<redacted>", result.Error, StringComparison.Ordinal);
        Assert.Contains("Password=<redacted>", result.Error, StringComparison.Ordinal);
        Assert.Contains("Authorization=<redacted>", result.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("super-secret", result.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("secret.example", result.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("auth-token", result.Error, StringComparison.Ordinal);
    }

    private sealed record TestMessage(string Value);

    private sealed class RecordingOutboxStore : ISqlMiniBusOutboxStore
    {
        private readonly Queue<IReadOnlyList<MiniBusOutboxStoredOperation>> _batches = new();

        public RecordingOutboxStore(params MiniBusOutboxStoredOperation[] operations)
        {
            _batches.Enqueue(operations);
        }

        public List<int> ClaimedBatchSizes { get; } = new();

        public List<Guid> MarkedDispatched { get; } = new();

        public Task<IReadOnlyList<MiniBusOutboxStoredOperation>> ClaimPendingAsync(
            int batchSize,
            CancellationToken cancellationToken = default)
        {
            ClaimedBatchSizes.Add(batchSize);

            return Task.FromResult(
                _batches.Count == 0
                    ? (IReadOnlyList<MiniBusOutboxStoredOperation>)Array.Empty<MiniBusOutboxStoredOperation>()
                    : _batches.Dequeue());
        }

        public Task MarkDispatchedAsync(
            Guid operationId,
            CancellationToken cancellationToken = default)
        {
            MarkedDispatched.Add(operationId);
            return Task.CompletedTask;
        }

        public Task MarkFailedAsync(
            Guid operationId,
            Exception exception,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<int> CleanupAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(0);
        }
    }

    private sealed class FailingOutboxStore : ISqlMiniBusOutboxStore
    {
        private readonly string _message;

        public FailingOutboxStore(string message)
        {
            _message = message;
        }

        public Task<IReadOnlyList<MiniBusOutboxStoredOperation>> ClaimPendingAsync(
            int batchSize,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException(_message);
        }

        public Task MarkDispatchedAsync(
            Guid operationId,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task MarkFailedAsync(
            Guid operationId,
            Exception exception,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<int> CleanupAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(0);
        }
    }

    private sealed class RecordingOutboxDispatcher : IMiniBusOutboxDispatcher
    {
        public List<Guid> Dispatched { get; } = new();

        public Task DispatchAsync(
            MiniBusOutboxStoredOperation operation,
            CancellationToken cancellationToken = default)
        {
            Dispatched.Add(operation.Id);
            return Task.CompletedTask;
        }
    }
}
