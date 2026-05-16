using Microsoft.Extensions.Logging;

namespace MiniBus.AzureFunctions.Processing.Pipeline;

internal static class MiniBusProcessingLogEvents
{
    public static readonly EventId ProcessingStarted = new(1000, nameof(ProcessingStarted));
    public static readonly EventId ProcessingCompleted = new(1001, nameof(ProcessingCompleted));
    public static readonly EventId ProcessingSkippedDuplicate = new(1002, nameof(ProcessingSkippedDuplicate));
    public static readonly EventId ProcessingRetried = new(1003, nameof(ProcessingRetried));
    public static readonly EventId ProcessingDelayedRetryScheduled = new(1004, nameof(ProcessingDelayedRetryScheduled));
    public static readonly EventId ProcessingDeadLettered = new(1005, nameof(ProcessingDeadLettered));
    public static readonly EventId ProcessingFailed = new(1006, nameof(ProcessingFailed));
    public static readonly EventId HandlerInvoked = new(1100, nameof(HandlerInvoked));
    public static readonly EventId SagaInvoked = new(1200, nameof(SagaInvoked));
    public static readonly EventId SagaCompleted = new(1201, nameof(SagaCompleted));
    public static readonly EventId OutboxCommitted = new(1300, nameof(OutboxCommitted));
}
