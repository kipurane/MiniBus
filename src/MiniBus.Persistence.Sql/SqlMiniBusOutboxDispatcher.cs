using MiniBus.Core.Persistence;

namespace MiniBus.Persistence.Sql;

public sealed class SqlMiniBusOutboxDispatcher
{
    private readonly ISqlMiniBusOutboxStore _store;
    private readonly IMiniBusOutboxDispatcher _dispatcher;
    private readonly MiniBusSqlPersistenceOptions _options;
    private readonly SqlMiniBusOutboxMetrics _metrics;

    public SqlMiniBusOutboxDispatcher(
        ISqlMiniBusOutboxStore store,
        IMiniBusOutboxDispatcher dispatcher,
        MiniBusSqlPersistenceOptions options)
        : this(store, dispatcher, options, new SqlMiniBusOutboxMetrics())
    {
    }

    internal SqlMiniBusOutboxDispatcher(
        ISqlMiniBusOutboxStore store,
        IMiniBusOutboxDispatcher dispatcher,
        MiniBusSqlPersistenceOptions options,
        SqlMiniBusOutboxMetrics metrics)
    {
        _store = store;
        _dispatcher = dispatcher;
        _options = options;
        _metrics = metrics;
    }

    public async Task<int> DispatchPendingAsync(CancellationToken cancellationToken = default)
    {
        return await DispatchPendingBatchesAsync(maxBatches: 1, cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> DispatchPendingBatchesAsync(
        int maxBatches,
        CancellationToken cancellationToken = default)
    {
        var dispatched = 0;

        if (maxBatches <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxBatches),
                maxBatches,
                "The maximum number of SQL outbox dispatch batches must be greater than zero.");
        }

        for (var batchAttemptCount = 0; batchAttemptCount < maxBatches; batchAttemptCount++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = await DispatchPendingBatchAsync(cancellationToken).ConfigureAwait(false);
            dispatched += result.DispatchedCount;

            if (result.ClaimedCount == 0 || result.FailedCount > 0)
            {
                break;
            }
        }

        return dispatched;
    }

    internal async Task<SqlMiniBusOutboxDispatchResult> DispatchPendingBatchAsync(
        CancellationToken cancellationToken = default)
    {
        var batchScope = _metrics.StartBatch();
        IReadOnlyList<MiniBusOutboxStoredOperation>? operations = null;
        var dispatched = 0;
        var failed = 0;

        try
        {
            operations = await _store
                .ClaimPendingAsync(_options.DispatcherBatchSize, cancellationToken)
                .ConfigureAwait(false);
            var claimedCount = operations.Count;

            foreach (var operation in operations)
            {
                var operationScope = _metrics.StartOperation();
                try
                {
                    await _dispatcher.DispatchAsync(operation, cancellationToken).ConfigureAwait(false);
                    await _store.MarkDispatchedAsync(operation.Id, cancellationToken).ConfigureAwait(false);
                    dispatched++;
                    _metrics.RecordOperation(operation, operationScope, SqlMiniBusOutboxDispatchOutcomes.Succeeded);
                }
                catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
                {
                    failed++;
                    try
                    {
                        await _store.MarkFailedAsync(operation.Id, exception, cancellationToken).ConfigureAwait(false);
                    }
                    finally
                    {
                        _metrics.RecordOperation(operation, operationScope, SqlMiniBusOutboxDispatchOutcomes.Failed);
                    }
                }
            }

            var batchOutcome = claimedCount == 0
                ? SqlMiniBusOutboxDispatchOutcomes.Empty
                : failed > 0
                    ? SqlMiniBusOutboxDispatchOutcomes.Failed
                    : SqlMiniBusOutboxDispatchOutcomes.Succeeded;

            _metrics.RecordBatch(
                batchScope,
                claimedCount,
                dispatched,
                failed,
                batchOutcome);

            return new SqlMiniBusOutboxDispatchResult(
                claimedCount,
                dispatched,
                failed);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _metrics.RecordBatch(
                batchScope,
                operations?.Count ?? 0,
                dispatched,
                failed,
                SqlMiniBusOutboxDispatchOutcomes.Failed);
            throw;
        }
    }
}
