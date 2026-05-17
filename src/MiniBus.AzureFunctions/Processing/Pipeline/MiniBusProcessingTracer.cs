using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using MiniBus.AzureServiceBus.TransportMessageMapping;
using MiniBus.Core.Recoverability;

namespace MiniBus.AzureFunctions.Processing.Pipeline;

internal sealed class MiniBusProcessingTracer
{
    // Stable ActivitySource used by applications in AddSource("MiniBus.Processing").
    // Changing this should be treated as an observability contract change.
    public const string SourceName = "MiniBus.Processing";

    // Stable root processing activity name.
    public const string ProcessActivityName = "MiniBus.Process";

    private const string MessagingSystemAzureServiceBus = "azure_service_bus";

    // ActivitySource is intentionally process-lifetime infrastructure. MiniBus processors share this static source
    // so host OpenTelemetry configuration can subscribe once with AddSource(SourceName). Disposing it from an
    // individual processor would break later processors in the same app, so the source is left for process teardown.
    [SuppressMessage(
        "Usage",
        "CA2213:Disposable fields should be disposed",
        Justification = "The shared ActivitySource is process-lifetime diagnostic infrastructure and must not be disposed by individual processors.")]
    private static readonly string SourceVersion = GetSourceVersion();

    private static readonly ActivitySource Source =
        new(SourceName, SourceVersion);

    public Activity? StartProcessingActivity(MiniBusProcessingContext context)
    {
        var activity = Source.StartActivity(ProcessActivityName, ActivityKind.Consumer);
        if (activity is null)
        {
            return null;
        }

        context.ProcessingActivity = activity;
        AddInitialTags(activity, context);
        return activity;
    }

    public void ProcessingCompleted(MiniBusProcessingContext context)
    {
        SetOutcome(context, MiniBusProcessingOutcomes.Completed);
    }

    public void ProcessingSkippedDuplicate(MiniBusProcessingContext context)
    {
        SetOutcome(context, MiniBusProcessingOutcomes.SkippedDuplicate);
        AddIfNotEmpty(context.ProcessingActivity, MiniBusProcessingTraceTags.MiniBusMessageId, context.InboxMessage?.MessageId);
        AddEvent(context, MiniBusProcessingTraceEvents.SkippedDuplicate);
    }

    public void ProcessingRetried(MiniBusProcessingContext context, Exception exception)
    {
        SetOutcome(context, MiniBusProcessingOutcomes.Retried);
        AddExceptionTags(context.ProcessingActivity, exception);
        AddEvent(context, MiniBusProcessingTraceEvents.Retried, tags => AddExceptionTags(tags, exception));
    }

    public void ProcessingDelayedRetryScheduled(MiniBusProcessingContext context)
    {
        SetOutcome(context, MiniBusProcessingOutcomes.DelayedRetryScheduled);
        AddEvent(context, MiniBusProcessingTraceEvents.DelayedRetryScheduled);
    }

    public void ProcessingDeadLettered(
        MiniBusProcessingContext context,
        string? deadLetterReason,
        string? deadLetterDescription)
    {
        SetOutcome(context, MiniBusProcessingOutcomes.DeadLettered);
        AddIfNotEmpty(context.ProcessingActivity, MiniBusProcessingTraceTags.MiniBusDeadLetterReason, deadLetterReason);
        AddIfNotEmpty(context.ProcessingActivity, MiniBusProcessingTraceTags.MiniBusDeadLetterDescription, deadLetterDescription);
        context.ProcessingActivity?.SetStatus(ActivityStatusCode.Error, deadLetterReason);
        AddEvent(
            context,
            MiniBusProcessingTraceEvents.DeadLettered,
            tags =>
            {
                AddIfNotEmpty(tags, MiniBusProcessingTraceTags.MiniBusDeadLetterReason, deadLetterReason);
                AddIfNotEmpty(tags, MiniBusProcessingTraceTags.MiniBusDeadLetterDescription, deadLetterDescription);
            });
    }

    public void ProcessingFailed(MiniBusProcessingContext context, Exception exception)
    {
        SetOutcome(context, MiniBusProcessingOutcomes.Failed);
        AddExceptionTags(context.ProcessingActivity, exception);
        context.ProcessingActivity?.SetStatus(ActivityStatusCode.Error, exception.Message);
        AddEvent(context, MiniBusProcessingTraceEvents.Failed, tags => AddExceptionTags(tags, exception));
    }

    public void HandlerInvoked(MiniBusProcessingContext context, Type handlerType)
    {
        AddIfNotEmpty(context.ProcessingActivity, MiniBusProcessingTraceTags.MiniBusHandlerType, handlerType.FullName);
        AddEvent(
            context,
            MiniBusProcessingTraceEvents.HandlerInvoked,
            tags => AddIfNotEmpty(tags, MiniBusProcessingTraceTags.MiniBusHandlerType, handlerType.FullName));
    }

    public void SagaInvoked(
        MiniBusProcessingContext context,
        Type sagaType,
        string sagaCorrelationId)
    {
        AddIfNotEmpty(context.ProcessingActivity, MiniBusProcessingTraceTags.MiniBusSagaType, sagaType.FullName);
        AddIfNotEmpty(context.ProcessingActivity, MiniBusProcessingTraceTags.MiniBusSagaCorrelationId, sagaCorrelationId);
        AddEvent(
            context,
            MiniBusProcessingTraceEvents.SagaInvoked,
            tags =>
            {
                AddIfNotEmpty(tags, MiniBusProcessingTraceTags.MiniBusSagaType, sagaType.FullName);
                AddIfNotEmpty(tags, MiniBusProcessingTraceTags.MiniBusSagaCorrelationId, sagaCorrelationId);
            });
    }

    public void SagaCompleted(
        MiniBusProcessingContext context,
        Type sagaType,
        string sagaCorrelationId)
    {
        AddIfNotEmpty(context.ProcessingActivity, MiniBusProcessingTraceTags.MiniBusSagaType, sagaType.FullName);
        AddIfNotEmpty(context.ProcessingActivity, MiniBusProcessingTraceTags.MiniBusSagaCorrelationId, sagaCorrelationId);
        AddEvent(
            context,
            MiniBusProcessingTraceEvents.SagaCompleted,
            tags =>
            {
                AddIfNotEmpty(tags, MiniBusProcessingTraceTags.MiniBusSagaType, sagaType.FullName);
                AddIfNotEmpty(tags, MiniBusProcessingTraceTags.MiniBusSagaCorrelationId, sagaCorrelationId);
            });
    }

    public void OutboxCommitted(MiniBusProcessingContext context)
    {
        if (context.OutboxOperations.Count == 0)
        {
            return;
        }

        context.ProcessingActivity?.SetTag(MiniBusProcessingTraceTags.MiniBusOutboxOperationCount, context.OutboxOperations.Count);
        AddEvent(
            context,
            MiniBusProcessingTraceEvents.OutboxCommitted,
            tags => tags.Add(new KeyValuePair<string, object?>(
                MiniBusProcessingTraceTags.MiniBusOutboxOperationCount,
                context.OutboxOperations.Count)));
    }

    private static void SetOutcome(
        MiniBusProcessingContext context,
        string outcome)
    {
        if (context.ProcessingActivity is not { } activity)
        {
            return;
        }

        AddEnrichedTags(activity, context);
        activity.SetTag(MiniBusProcessingTraceTags.MiniBusProcessingOutcome, outcome);
    }

    private static void AddInitialTags(
        Activity activity,
        MiniBusProcessingContext context)
    {
        activity.SetTag(MiniBusProcessingTraceTags.MessagingSystem, MessagingSystemAzureServiceBus);
        AddIfNotEmpty(activity, MiniBusProcessingTraceTags.MiniBusEndpoint, context.Options.EndpointName);
        AddIfNotEmpty(
            activity,
            MiniBusProcessingTraceTags.MiniBusMessageType,
            context.MessageType?.FullName ?? GetHeaderOrValue(context, MiniBusHeaderNames.MessageType, context.Message.Subject));
        AddIfNotEmpty(
            activity,
            MiniBusProcessingTraceTags.MiniBusMessageId,
            GetHeaderOrValue(context, MiniBusHeaderNames.MessageId, context.Message.MessageId));
        AddIfNotEmpty(
            activity,
            MiniBusProcessingTraceTags.MiniBusCorrelationId,
            GetHeaderOrValue(context, MiniBusHeaderNames.CorrelationId, context.Message.CorrelationId));
        AddIfNotEmpty(
            activity,
            MiniBusProcessingTraceTags.MiniBusCausationId,
            GetHeaderOrValue(context, MiniBusHeaderNames.CausationId, null));
        AddIfNotEmpty(
            activity,
            MiniBusProcessingTraceTags.MiniBusRetryAttempt,
            GetHeaderOrValue(context, MiniBusRecoverabilityHeaderNames.ImmediateAttempt, null));
        AddIfNotEmpty(
            activity,
            MiniBusProcessingTraceTags.MiniBusDelayedRetryAttempt,
            GetHeaderOrValue(context, MiniBusRecoverabilityHeaderNames.DelayedAttempt, null));
    }

    private static void AddEnrichedTags(
        Activity activity,
        MiniBusProcessingContext context)
    {
        AddIfNotEmpty(activity, MiniBusProcessingTraceTags.MiniBusMessageType, context.MessageType?.FullName);
        AddIfNotEmpty(
            activity,
            MiniBusProcessingTraceTags.MiniBusRetryAttempt,
            GetHeaderOrValue(context, MiniBusRecoverabilityHeaderNames.ImmediateAttempt, null));
        AddIfNotEmpty(
            activity,
            MiniBusProcessingTraceTags.MiniBusDelayedRetryAttempt,
            GetHeaderOrValue(context, MiniBusRecoverabilityHeaderNames.DelayedAttempt, null));
        AddIfNotEmpty(activity, MiniBusProcessingTraceTags.MiniBusHandlerType, context.LastHandlerType?.FullName);
        AddIfNotEmpty(activity, MiniBusProcessingTraceTags.MiniBusSagaType, context.LastSagaType?.FullName);
        AddIfNotEmpty(activity, MiniBusProcessingTraceTags.MiniBusSagaCorrelationId, context.LastSagaCorrelationId);
    }

    private static string? GetHeaderOrValue(
        MiniBusProcessingContext context,
        string headerName,
        string? fallback)
    {
        return context.Headers.TryGetValue(headerName, out var value)
            ? NormalizeWhitespaceToNull(value)
            : NormalizeWhitespaceToNull(fallback);
    }

    private static string? NormalizeWhitespaceToNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string GetSourceVersion()
    {
        try
        {
            var assembly = typeof(MiniBusProcessingTracer).Assembly;
            var informationalVersion = assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion;

            if (!string.IsNullOrWhiteSpace(informationalVersion))
            {
                return informationalVersion;
            }

            return assembly.GetName().Version?.ToString() ?? "0.0.0";
        }
        catch
        {
            return "0.0.0";
        }
    }

    private static void AddEvent(
        MiniBusProcessingContext context,
        string name,
        Action<List<KeyValuePair<string, object?>>>? addTags = null)
    {
        if (context.ProcessingActivity is not { } activity)
        {
            return;
        }

        var tags = new List<KeyValuePair<string, object?>>();
        addTags?.Invoke(tags);
        activity.AddEvent(tags.Count == 0
            ? new ActivityEvent(name)
            : new ActivityEvent(name, tags: new ActivityTagsCollection(tags)));
    }

    private static void AddExceptionTags(
        Activity? activity,
        Exception exception)
    {
        if (activity is null)
        {
            return;
        }

        AddIfNotEmpty(activity, MiniBusProcessingTraceTags.ExceptionType, exception.GetType().FullName);
        AddIfNotEmpty(activity, MiniBusProcessingTraceTags.ExceptionMessage, exception.Message);
    }

    private static void AddExceptionTags(
        ICollection<KeyValuePair<string, object?>> tags,
        Exception exception)
    {
        AddIfNotEmpty(tags, MiniBusProcessingTraceTags.ExceptionType, exception.GetType().FullName);
        AddIfNotEmpty(tags, MiniBusProcessingTraceTags.ExceptionMessage, exception.Message);
    }

    private static void AddIfNotEmpty(
        Activity? activity,
        string key,
        string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            activity?.SetTag(key, value);
        }
    }

    private static void AddIfNotEmpty(
        ICollection<KeyValuePair<string, object?>> tags,
        string key,
        string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            tags.Add(new KeyValuePair<string, object?>(key, value));
        }
    }
}
