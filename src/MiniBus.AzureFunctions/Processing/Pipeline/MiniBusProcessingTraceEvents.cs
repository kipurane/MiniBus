namespace MiniBus.AzureFunctions.Processing.Pipeline;

internal static class MiniBusProcessingTraceEvents
{
    public const string HandlerInvoked = "minibus.handler.invoked";
    public const string SagaInvoked = "minibus.saga.invoked";
    public const string SagaCompleted = "minibus.saga.completed";
    public const string OutboxCommitted = "minibus.outbox.committed";
    public const string Retried = "minibus.processing.retried";
    public const string DelayedRetryScheduled = "minibus.processing.delayed_retry_scheduled";
    public const string DeadLettered = "minibus.processing.dead_lettered";
    public const string SkippedDuplicate = "minibus.processing.skipped_duplicate";
    public const string Failed = "minibus.processing.failed";
}