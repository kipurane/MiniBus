using MiniBus.Core.Persistence;

namespace MiniBus.Persistence.Sql;

internal interface ISqlMiniBusOutboxBatchStore
{
    Task<SqlMiniBusOutboxClaimResult> ClaimBatchAsync(
        int batchSize,
        CancellationToken cancellationToken);
}

// Decode failures have already been recorded and released by the store.
internal sealed record SqlMiniBusOutboxClaimResult(
    IReadOnlyList<MiniBusOutboxStoredOperation> Operations,
    int FailedCount,
    Exception? FirstFailure)
{
    public int ClaimedCount => Operations.Count + FailedCount;

    public void ThrowIfAllFailed()
    {
        if (FailedCount > 0 && Operations.Count == 0)
        {
            throw new InvalidOperationException("MiniBus could not deserialize claimed outbox operations.", FirstFailure);
        }
    }
}
