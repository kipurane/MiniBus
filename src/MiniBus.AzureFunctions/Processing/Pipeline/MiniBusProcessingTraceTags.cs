namespace MiniBus.AzureFunctions.Processing.Pipeline;

internal static class MiniBusProcessingTraceTags
{
    // Stable Activity tag names. Changing these should be treated as an observability contract change.
    public const string MessagingSystem = "messaging.system";
    public const string MiniBusEndpoint = "minibus.endpoint";
    public const string MiniBusMessageType = "minibus.message_type";
    public const string MiniBusMessageId = "minibus.message_id";
    public const string MiniBusCorrelationId = "minibus.correlation_id";
    public const string MiniBusCausationId = "minibus.causation_id";
    public const string MiniBusRetryAttempt = "minibus.retry_attempt";
    public const string MiniBusDelayedRetryAttempt = "minibus.delayed_retry_attempt";
    public const string MiniBusHandlerType = "minibus.handler_type";
    public const string MiniBusSagaType = "minibus.saga_type";
    public const string MiniBusSagaCorrelationId = "minibus.saga_correlation_id";
    public const string MiniBusProcessingOutcome = "minibus.processing_outcome";
    public const string MiniBusOutboxOperationCount = "minibus.outbox_operation_count";
    public const string MiniBusDeadLetterReason = "minibus.dead_letter_reason";
    public const string MiniBusDeadLetterDescription = "minibus.dead_letter_description";
    public const string ExceptionType = "exception.type";
    public const string ExceptionMessage = "exception.message";
}