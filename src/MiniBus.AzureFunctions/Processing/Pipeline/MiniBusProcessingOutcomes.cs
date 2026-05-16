namespace MiniBus.AzureFunctions.Processing.Pipeline;

internal static class MiniBusProcessingOutcomes
{
    public const string Started = "started";
    public const string Completed = "completed";
    public const string Retried = "retried";
    public const string DelayedRetryScheduled = "delayed-retry-scheduled";
    public const string DeadLettered = "dead-lettered";
    public const string SkippedDuplicate = "skipped-duplicate";
    public const string Failed = "failed";
    public const string HandlerInvoked = "handler-invoked";
    public const string SagaInvoked = "saga-invoked";
    public const string SagaCompleted = "saga-completed";
    public const string OutboxCommitted = "outbox-committed";
}
