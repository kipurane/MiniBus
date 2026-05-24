namespace MiniBus.Persistence.Sql;

internal sealed record SqlMiniBusOutboxDispatchResult(
    int ClaimedCount,
    int DispatchedCount,
    int FailedCount);
