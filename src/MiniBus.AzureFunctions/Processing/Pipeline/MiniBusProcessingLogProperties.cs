namespace MiniBus.AzureFunctions.Processing.Pipeline;

internal static class MiniBusProcessingLogProperties
{
    public const string EndpointName = nameof(EndpointName);
    public const string MessageType = nameof(MessageType);
    public const string MessageId = nameof(MessageId);
    public const string LogicalMessageId = nameof(LogicalMessageId);
    public const string CorrelationId = nameof(CorrelationId);
    public const string CausationId = nameof(CausationId);
    public const string RetryAttempt = nameof(RetryAttempt);
    public const string DelayedRetryAttempt = nameof(DelayedRetryAttempt);
    public const string HandlerType = nameof(HandlerType);
    public const string SagaType = nameof(SagaType);
    public const string SagaCorrelationId = nameof(SagaCorrelationId);
    public const string ProcessingOutcome = nameof(ProcessingOutcome);
    public const string OutboxOperationCount = nameof(OutboxOperationCount);
    public const string DeadLetterReason = nameof(DeadLetterReason);
    public const string DeadLetterDescription = nameof(DeadLetterDescription);
    public const string ExceptionType = nameof(ExceptionType);
}
