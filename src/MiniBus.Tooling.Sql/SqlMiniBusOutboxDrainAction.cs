using MiniBus.Persistence.Sql;
using MiniBus.Tooling.Core;

namespace MiniBus.Tooling.Sql;

public sealed class SqlMiniBusOutboxDrainAction : IMiniBusOutboxDrainAction
{
    private const int FailureSummaryMaxLength = 240;

    private readonly SqlMiniBusOutboxDispatcher _dispatcher;

    public SqlMiniBusOutboxDrainAction(SqlMiniBusOutboxDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        _dispatcher = dispatcher;
    }

    public async Task<MiniBusOutboxDrainResult> DrainAsync(
        MiniBusOutboxDrainRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validation = request.Validate();
        if (!validation.IsValid)
        {
            return MiniBusOutboxDrainResult.Failure(validation.Error!);
        }

        try
        {
            var dispatched = await _dispatcher
                .DispatchPendingBatchesAsync(request.MaxBatches, cancellationToken)
                .ConfigureAwait(false);

            return MiniBusOutboxDrainResult.Success(dispatched);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            var safeSummary = SqlToolingTextRedactor.RedactAndTruncate(
                exception.Message,
                FailureSummaryMaxLength);

            return MiniBusOutboxDrainResult.Failure(
                "SQL outbox drain failed. "
                + "Error code: SQL_OUTBOX_DRAIN_FAILED. "
                + $"Exception type: {exception.GetType().Name}. "
                + $"Safe summary: {safeSummary}");
        }
    }
}
