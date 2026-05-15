namespace MiniBus.Core.Auditing;

public enum MiniBusAuditProcessingOutcome
{
    Completed,
    SkippedDuplicate,
    DelayedRetryScheduled,
    DeadLettered
}
