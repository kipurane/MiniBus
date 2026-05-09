using MiniBus.Core.Persistence;

namespace MiniBus.Persistence.Sql;

public sealed class SqlMiniBusOutboxDispatcher
{
    private readonly ISqlMiniBusOutboxStore _store;
    private readonly IMiniBusOutboxDispatcher _dispatcher;
    private readonly MiniBusSqlPersistenceOptions _options;

    public SqlMiniBusOutboxDispatcher(
        ISqlMiniBusOutboxStore store,
        IMiniBusOutboxDispatcher dispatcher,
        MiniBusSqlPersistenceOptions options)
    {
        _store = store;
        _dispatcher = dispatcher;
        _options = options;
    }

    public async Task<int> DispatchPendingAsync(CancellationToken cancellationToken = default)
    {
        var operations = await _store
            .ClaimPendingAsync(_options.DispatcherBatchSize, cancellationToken)
            .ConfigureAwait(false);
        var dispatched = 0;

        foreach (var operation in operations)
        {
            try
            {
                await _dispatcher.DispatchAsync(operation, cancellationToken).ConfigureAwait(false);
                await _store.MarkDispatchedAsync(operation.Id, cancellationToken).ConfigureAwait(false);
                dispatched++;
            }
            catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                await _store.MarkFailedAsync(operation.Id, exception, cancellationToken).ConfigureAwait(false);
            }
        }

        return dispatched;
    }
}
