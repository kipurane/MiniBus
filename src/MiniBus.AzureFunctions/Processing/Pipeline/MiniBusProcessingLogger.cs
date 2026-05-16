using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MiniBus.AzureServiceBus.TransportMessageMapping;
using MiniBus.Core.Recoverability;

namespace MiniBus.AzureFunctions.Processing.Pipeline;

internal sealed class MiniBusProcessingLogger
{
    // Stable logging category used by applications for MiniBus processing filters.
    // Changing it should be treated as an observability contract change.
    private const string CategoryName = "MiniBus.Processing";
    private const string UnknownOutcomeMessage = "MiniBus processing unknown outcome";
    private const string ProcessingStartedMessage = "MiniBus processing started";
    private const string ProcessingCompletedMessage = "MiniBus processing completed";
    private const string ProcessingSkippedDuplicateMessage = "MiniBus processing skipped-duplicate";
    private const string ProcessingRetriedMessage = "MiniBus processing retried";
    private const string ProcessingDelayedRetryScheduledMessage = "MiniBus processing delayed-retry-scheduled";
    private const string ProcessingDeadLetteredMessage = "MiniBus processing dead-lettered";
    private const string ProcessingFailedMessage = "MiniBus processing failed";
    private const string HandlerInvokedMessage = "MiniBus processing handler-invoked";
    private const string SagaInvokedMessage = "MiniBus processing saga-invoked";
    private const string SagaCompletedMessage = "MiniBus processing saga-completed";
    private const string OutboxCommittedMessage = "MiniBus processing outbox-committed";
    private static readonly Func<IReadOnlyDictionary<string, object?>, Exception?, string> FormatLogMessage = Format;

    private readonly ILogger _logger;

    public MiniBusProcessingLogger(ILoggerFactory? loggerFactory)
    {
        _logger = loggerFactory?.CreateLogger(CategoryName) ?? NullLogger.Instance;
    }

    public IDisposable BeginProcessingScope(MiniBusProcessingContext context)
    {
        var scopeState = CreateBaseState(context);
        return _logger.BeginScope(scopeState) ?? NullLogger.Instance.BeginScope(scopeState);
    }

    public bool IsHandlerInvocationEnabled()
    {
        return _logger.IsEnabled(LogLevel.Information);
    }

    public void ProcessingStarted(MiniBusProcessingContext context)
    {
        Log(
            LogLevel.Information,
            MiniBusProcessingLogEvents.ProcessingStarted,
            context,
            MiniBusProcessingOutcomes.Started,
            null);
    }

    public void ProcessingCompleted(MiniBusProcessingContext context)
    {
        Log(
            LogLevel.Information,
            MiniBusProcessingLogEvents.ProcessingCompleted,
            context,
            MiniBusProcessingOutcomes.Completed,
            null,
            state => AddOutboxOperationCount(state, context));
    }

    public void ProcessingSkippedDuplicate(MiniBusProcessingContext context)
    {
        Log(
            LogLevel.Warning,
            MiniBusProcessingLogEvents.ProcessingSkippedDuplicate,
            context,
            MiniBusProcessingOutcomes.SkippedDuplicate,
            null,
            state =>
            {
                AddOutboxOperationCount(state, context);
                AddIfNotEmpty(state, MiniBusProcessingLogProperties.LogicalMessageId, context.InboxMessage?.MessageId);
            });
    }

    public void ProcessingRetried(MiniBusProcessingContext context, Exception exception)
    {
        Log(
            LogLevel.Warning,
            MiniBusProcessingLogEvents.ProcessingRetried,
            context,
            MiniBusProcessingOutcomes.Retried,
            exception,
            state => AddIfNotEmpty(state, MiniBusProcessingLogProperties.ExceptionType, exception.GetType().FullName));
    }

    public void ProcessingDelayedRetryScheduled(MiniBusProcessingContext context)
    {
        Log(
            LogLevel.Warning,
            MiniBusProcessingLogEvents.ProcessingDelayedRetryScheduled,
            context,
            MiniBusProcessingOutcomes.DelayedRetryScheduled,
            null);
    }

    public void ProcessingDeadLettered(
        MiniBusProcessingContext context,
        string? deadLetterReason,
        string? deadLetterDescription)
    {
        Log(
            LogLevel.Warning,
            MiniBusProcessingLogEvents.ProcessingDeadLettered,
            context,
            MiniBusProcessingOutcomes.DeadLettered,
            null,
            state =>
            {
                AddIfNotEmpty(state, MiniBusProcessingLogProperties.DeadLetterReason, deadLetterReason);
                AddIfNotEmpty(state, MiniBusProcessingLogProperties.DeadLetterDescription, deadLetterDescription);
            });
    }

    public void ProcessingFailed(MiniBusProcessingContext context, Exception exception)
    {
        Log(
            LogLevel.Error,
            MiniBusProcessingLogEvents.ProcessingFailed,
            context,
            MiniBusProcessingOutcomes.Failed,
            exception,
            state => AddIfNotEmpty(state, MiniBusProcessingLogProperties.ExceptionType, exception.GetType().FullName));
    }

    public void HandlerInvoked(MiniBusProcessingContext context, Type handlerType)
    {
        context.LastHandlerType = handlerType;
        Log(
            LogLevel.Information,
            MiniBusProcessingLogEvents.HandlerInvoked,
            context,
            MiniBusProcessingOutcomes.HandlerInvoked,
            null,
            state => AddIfNotEmpty(state, MiniBusProcessingLogProperties.HandlerType, handlerType.FullName));
    }

    public void SagaInvoked(
        MiniBusProcessingContext context,
        Type sagaType,
        string sagaCorrelationId)
    {
        context.LastSagaType = sagaType;
        context.LastSagaCorrelationId = sagaCorrelationId;
        Log(
            LogLevel.Information,
            MiniBusProcessingLogEvents.SagaInvoked,
            context,
            MiniBusProcessingOutcomes.SagaInvoked,
            null,
            state =>
            {
                AddIfNotEmpty(state, MiniBusProcessingLogProperties.SagaType, sagaType.FullName);
                AddIfNotEmpty(state, MiniBusProcessingLogProperties.SagaCorrelationId, sagaCorrelationId);
            });
    }

    public void SagaCompleted(
        MiniBusProcessingContext context,
        Type sagaType,
        string sagaCorrelationId)
    {
        context.LastSagaType = sagaType;
        context.LastSagaCorrelationId = sagaCorrelationId;
        Log(
            LogLevel.Information,
            MiniBusProcessingLogEvents.SagaCompleted,
            context,
            MiniBusProcessingOutcomes.SagaCompleted,
            null,
            state =>
            {
                AddIfNotEmpty(state, MiniBusProcessingLogProperties.SagaType, sagaType.FullName);
                AddIfNotEmpty(state, MiniBusProcessingLogProperties.SagaCorrelationId, sagaCorrelationId);
            });
    }

    public void OutboxCommitted(MiniBusProcessingContext context)
    {
        if (context.OutboxOperations.Count == 0)
        {
            return;
        }

        Log(
            LogLevel.Information,
            MiniBusProcessingLogEvents.OutboxCommitted,
            context,
            MiniBusProcessingOutcomes.OutboxCommitted,
            null,
            state => AddOutboxOperationCount(state, context));
    }

    private void Log(
        LogLevel logLevel,
        EventId eventId,
        MiniBusProcessingContext context,
        string outcome,
        Exception? exception,
        Action<Dictionary<string, object?>>? addState = null)
    {
        if (!_logger.IsEnabled(logLevel))
        {
            return;
        }

        var state = CreateBaseState(context);
        state[MiniBusProcessingLogProperties.ProcessingOutcome] = outcome;
        addState?.Invoke(state);

        _logger.Log(
            logLevel,
            eventId,
            state,
            exception,
            FormatLogMessage);
    }

    private static string Format(
        IReadOnlyDictionary<string, object?> state,
        Exception? _)
    {
        var outcome = state.TryGetValue(MiniBusProcessingLogProperties.ProcessingOutcome, out var value)
                      && value is string outcomeValue
                      && !string.IsNullOrWhiteSpace(outcomeValue)
            ? outcomeValue
            : null;

        return outcome switch
        {
            MiniBusProcessingOutcomes.Started => ProcessingStartedMessage,
            MiniBusProcessingOutcomes.Completed => ProcessingCompletedMessage,
            MiniBusProcessingOutcomes.SkippedDuplicate => ProcessingSkippedDuplicateMessage,
            MiniBusProcessingOutcomes.Retried => ProcessingRetriedMessage,
            MiniBusProcessingOutcomes.DelayedRetryScheduled => ProcessingDelayedRetryScheduledMessage,
            MiniBusProcessingOutcomes.DeadLettered => ProcessingDeadLetteredMessage,
            MiniBusProcessingOutcomes.Failed => ProcessingFailedMessage,
            MiniBusProcessingOutcomes.HandlerInvoked => HandlerInvokedMessage,
            MiniBusProcessingOutcomes.SagaInvoked => SagaInvokedMessage,
            MiniBusProcessingOutcomes.SagaCompleted => SagaCompletedMessage,
            MiniBusProcessingOutcomes.OutboxCommitted => OutboxCommittedMessage,
            _ => UnknownOutcomeMessage
        };
    }

    private static Dictionary<string, object?> CreateBaseState(MiniBusProcessingContext context)
    {
        var state = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [MiniBusProcessingLogProperties.EndpointName] = context.Options.EndpointName,
            [MiniBusProcessingLogProperties.MessageId] = GetHeaderOrValue(
                context,
                MiniBusHeaderNames.MessageId,
                context.Message.MessageId)
        };

        AddIfNotEmpty(
            state,
            MiniBusProcessingLogProperties.MessageType,
            context.MessageType?.FullName ?? GetHeaderOrValue(context, MiniBusHeaderNames.MessageType, context.Message.Subject));
        AddIfNotEmpty(
            state,
            MiniBusProcessingLogProperties.CorrelationId,
            GetHeaderOrValue(context, MiniBusHeaderNames.CorrelationId, context.Message.CorrelationId));
        AddIfNotEmpty(
            state,
            MiniBusProcessingLogProperties.CausationId,
            GetHeaderOrValue(context, MiniBusHeaderNames.CausationId, null));
        AddIfNotEmpty(
            state,
            MiniBusProcessingLogProperties.RetryAttempt,
            GetHeaderOrValue(context, MiniBusRecoverabilityHeaderNames.ImmediateAttempt, null));
        AddIfNotEmpty(
            state,
            MiniBusProcessingLogProperties.DelayedRetryAttempt,
            GetHeaderOrValue(context, MiniBusRecoverabilityHeaderNames.DelayedAttempt, null));
        AddIfNotEmpty(state, MiniBusProcessingLogProperties.HandlerType, context.LastHandlerType?.FullName);
        AddIfNotEmpty(state, MiniBusProcessingLogProperties.SagaType, context.LastSagaType?.FullName);
        AddIfNotEmpty(state, MiniBusProcessingLogProperties.SagaCorrelationId, context.LastSagaCorrelationId);

        return state;
    }

    private static string? GetHeaderOrValue(
        MiniBusProcessingContext context,
        string headerName,
        string? fallback)
    {
        if (context.Headers.TryGetValue(headerName, out var value)
            && !string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return string.IsNullOrWhiteSpace(fallback) ? null : fallback;
    }

    private static void AddOutboxOperationCount(
        IDictionary<string, object?> state,
        MiniBusProcessingContext context)
    {
        if (context.OutboxOperations.Count > 0)
        {
            state[MiniBusProcessingLogProperties.OutboxOperationCount] = context.OutboxOperations.Count;
        }
    }

    private static void AddIfNotEmpty(
        IDictionary<string, object?> state,
        string key,
        string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            state[key] = value;
        }
    }

}
