namespace MiniBus.Persistence.Sql;

internal sealed record SqlMiniBusHostedOutboxDispatchCycleResult(
    int BatchAttemptCount,
    int ClaimedCount,
    int DispatchedCount,
    int FailedCount,
    bool BackoffRequired,
    bool MoreWorkMayBeAvailable);
