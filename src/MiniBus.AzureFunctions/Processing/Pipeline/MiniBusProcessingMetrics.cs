using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;
using System.Reflection;
using MiniBus.AzureServiceBus.TransportMessageMapping;
using MiniBus.Core.Sagas;

namespace MiniBus.AzureFunctions.Processing.Pipeline;

internal sealed class MiniBusProcessingMetrics
{
    // Stable Meter used by applications in AddMeter("MiniBus.Processing").
    // Changing this should be treated as an observability contract change.
    public const string MeterName = "MiniBus.Processing";

    // Stable instrument names, units, descriptions, and tag names are observability contracts.
    public const string ProcessingAttemptsInstrumentName = "minibus.processing.attempts";
    public const string ProcessingDurationInstrumentName = "minibus.processing.duration";
    public const string ProcessingRetriesInstrumentName = "minibus.processing.retries";
    public const string ProcessingDeadLettersInstrumentName = "minibus.processing.dead_letters";
    public const string ProcessingDuplicatesInstrumentName = "minibus.processing.duplicates";
    public const string ProcessingFailuresInstrumentName = "minibus.processing.failures";
    public const string HandlerDurationInstrumentName = "minibus.handler.duration";
    public const string SagaDurationInstrumentName = "minibus.saga.duration";
    public const string SagaCompletionsInstrumentName = "minibus.saga.completions";

    public const string AttemptsUnit = "{attempt}";
    public const string MessageUnit = "{message}";
    public const string RetryUnit = "{retry}";
    public const string FailureUnit = "{failure}";
    public const string SagaUnit = "{saga}";
    public const string DurationUnit = "s";

    [SuppressMessage(
        "Usage",
        "CA2213:Disposable fields should be disposed",
        Justification = "The shared Meter is process-lifetime diagnostic infrastructure and must not be disposed by individual processors.")]
    private static readonly string MeterVersion = GetMeterVersion();

    private static readonly Meter Meter = new(MeterName, MeterVersion);

    private static readonly Counter<long> ProcessingAttempts = Meter.CreateCounter<long>(
        ProcessingAttemptsInstrumentName,
        AttemptsUnit,
        "Number of MiniBus processing attempts.");

    private static readonly Histogram<double> ProcessingDuration = Meter.CreateHistogram<double>(
        ProcessingDurationInstrumentName,
        DurationUnit,
        "Duration of MiniBus processing attempts.");

    private static readonly Counter<long> ProcessingRetries = Meter.CreateCounter<long>(
        ProcessingRetriesInstrumentName,
        RetryUnit,
        "Number of MiniBus retry decisions.");

    private static readonly Counter<long> ProcessingDeadLetters = Meter.CreateCounter<long>(
        ProcessingDeadLettersInstrumentName,
        MessageUnit,
        "Number of MiniBus dead-letter outcomes.");

    private static readonly Counter<long> ProcessingDuplicates = Meter.CreateCounter<long>(
        ProcessingDuplicatesInstrumentName,
        MessageUnit,
        "Number of MiniBus duplicate message skips.");

    private static readonly Counter<long> ProcessingFailures = Meter.CreateCounter<long>(
        ProcessingFailuresInstrumentName,
        FailureUnit,
        "Number of MiniBus processing failures.");

    private static readonly Histogram<double> HandlerDuration = Meter.CreateHistogram<double>(
        HandlerDurationInstrumentName,
        DurationUnit,
        "Duration of MiniBus handler invocations.");

    private static readonly Histogram<double> SagaDuration = Meter.CreateHistogram<double>(
        SagaDurationInstrumentName,
        DurationUnit,
        "Duration of MiniBus saga invocations.");

    private static readonly Counter<long> SagaCompletions = Meter.CreateCounter<long>(
        SagaCompletionsInstrumentName,
        SagaUnit,
        "Number of MiniBus saga completions.");

    public MiniBusProcessingMetricAttempt? StartProcessingAttempt()
    {
        if (!ProcessingAttempts.Enabled && !ProcessingDuration.Enabled)
        {
            return null;
        }

        return new MiniBusProcessingMetricAttempt(Stopwatch.GetTimestamp());
    }

    public void ProcessingCompleted(MiniBusProcessingContext context)
    {
        RecordProcessing(context, MiniBusProcessingOutcomes.Completed);
    }

    public void ProcessingSkippedDuplicate(MiniBusProcessingContext context)
    {
        RecordProcessing(context, MiniBusProcessingOutcomes.SkippedDuplicate);

        if (ProcessingDuplicates.Enabled)
        {
            ProcessingDuplicates.Add(1, CreateProcessingTags(context, MiniBusProcessingOutcomes.SkippedDuplicate));
        }
    }

    public void ProcessingRetried(MiniBusProcessingContext context)
    {
        RecordProcessing(context, MiniBusProcessingOutcomes.Retried);

        if (ProcessingRetries.Enabled)
        {
            ProcessingRetries.Add(
                1,
                CreateRetryTags(context, MiniBusProcessingOutcomes.Retried, MiniBusProcessingRetryKinds.Immediate));
        }
    }

    public void ProcessingDelayedRetryScheduled(MiniBusProcessingContext context)
    {
        RecordProcessing(context, MiniBusProcessingOutcomes.DelayedRetryScheduled);

        if (ProcessingRetries.Enabled)
        {
            ProcessingRetries.Add(
                1,
                CreateRetryTags(
                    context,
                    MiniBusProcessingOutcomes.DelayedRetryScheduled,
                    MiniBusProcessingRetryKinds.Delayed));
        }
    }

    public void ProcessingDeadLettered(MiniBusProcessingContext context)
    {
        RecordProcessing(context, MiniBusProcessingOutcomes.DeadLettered);

        if (ProcessingDeadLetters.Enabled)
        {
            ProcessingDeadLetters.Add(1, CreateProcessingTags(context, MiniBusProcessingOutcomes.DeadLettered));
        }
    }

    public void ProcessingFailed(MiniBusProcessingContext context)
    {
        RecordProcessing(context, MiniBusProcessingOutcomes.Failed);

        if (ProcessingFailures.Enabled)
        {
            ProcessingFailures.Add(1, CreateProcessingTags(context, MiniBusProcessingOutcomes.Failed));
        }
    }

    public async Task MeasureHandlerInvocationAsync(
        MiniBusProcessingContext context,
        Type handlerType,
        Func<Task> invoke)
    {
        ArgumentNullException.ThrowIfNull(invoke);

        if (!HandlerDuration.Enabled)
        {
            await invoke().ConfigureAwait(false);
            return;
        }

        var start = Stopwatch.GetTimestamp();
        try
        {
            await invoke().ConfigureAwait(false);
            RecordHandlerDuration(context, handlerType, MiniBusProcessingMetricOutcomes.Completed, start);
        }
        catch
        {
            RecordHandlerDuration(context, handlerType, MiniBusProcessingMetricOutcomes.Failed, start);
            throw;
        }
    }

    public async Task<SagaInvocationDiagnostic> MeasureSagaInvocationAsync(
        MiniBusProcessingContext context,
        Type sagaType,
        Func<Task<SagaInvocationDiagnostic>> invoke)
    {
        ArgumentNullException.ThrowIfNull(invoke);

        if (!SagaDuration.Enabled && !SagaCompletions.Enabled)
        {
            return await invoke().ConfigureAwait(false);
        }

        var start = Stopwatch.GetTimestamp();
        try
        {
            var diagnostic = await invoke().ConfigureAwait(false);
            var outcome = diagnostic.Completed
                ? MiniBusProcessingMetricOutcomes.Completed
                : MiniBusProcessingMetricOutcomes.Handled;

            if (SagaDuration.Enabled)
            {
                SagaDuration.Record(
                    GetElapsedSeconds(start),
                    CreateSagaTags(context, sagaType, outcome));
            }

            if (diagnostic.Completed && SagaCompletions.Enabled)
            {
                SagaCompletions.Add(1, CreateSagaTags(context, sagaType, outcome));
            }

            return diagnostic;
        }
        catch
        {
            if (SagaDuration.Enabled)
            {
                SagaDuration.Record(
                    GetElapsedSeconds(start),
                    CreateSagaTags(context, sagaType, MiniBusProcessingMetricOutcomes.Failed));
            }

            throw;
        }
    }

    private static void RecordProcessing(
        MiniBusProcessingContext context,
        string outcome)
    {
        var tagsRequired = ProcessingAttempts.Enabled || ProcessingDuration.Enabled;
        if (!tagsRequired)
        {
            return;
        }

        var tags = CreateProcessingTags(context, outcome);
        if (ProcessingAttempts.Enabled)
        {
            ProcessingAttempts.Add(1, tags);
        }

        if (ProcessingDuration.Enabled && context.ProcessingMetricAttempt is { } attempt)
        {
            ProcessingDuration.Record(GetElapsedSeconds(attempt.StartTimestamp), tags);
        }
    }

    private static void RecordHandlerDuration(
        MiniBusProcessingContext context,
        Type handlerType,
        string outcome,
        long startTimestamp)
    {
        HandlerDuration.Record(
            GetElapsedSeconds(startTimestamp),
            CreateHandlerTags(context, handlerType, outcome));
    }

    private static TagList CreateProcessingTags(
        MiniBusProcessingContext context,
        string outcome)
    {
        var tags = CreateBaseTags(context);
        tags.Add(MiniBusProcessingMetricTags.MiniBusProcessingOutcome, outcome);
        return tags;
    }

    private static TagList CreateRetryTags(
        MiniBusProcessingContext context,
        string outcome,
        string retryKind)
    {
        var tags = CreateProcessingTags(context, outcome);
        tags.Add(MiniBusProcessingMetricTags.MiniBusRetryKind, retryKind);
        return tags;
    }

    private static TagList CreateHandlerTags(
        MiniBusProcessingContext context,
        Type handlerType,
        string outcome)
    {
        var tags = CreateBaseTags(context);
        AddIfNotEmpty(ref tags, MiniBusProcessingMetricTags.MiniBusHandlerType, handlerType.FullName);
        tags.Add(MiniBusProcessingMetricTags.MiniBusHandlerOutcome, outcome);
        return tags;
    }

    private static TagList CreateSagaTags(
        MiniBusProcessingContext context,
        Type sagaType,
        string outcome)
    {
        var tags = CreateBaseTags(context);
        AddIfNotEmpty(ref tags, MiniBusProcessingMetricTags.MiniBusSagaType, sagaType.FullName);
        tags.Add(MiniBusProcessingMetricTags.MiniBusSagaOutcome, outcome);
        return tags;
    }

    private static TagList CreateBaseTags(MiniBusProcessingContext context)
    {
        var tags = new TagList();
        AddIfNotEmpty(ref tags, MiniBusProcessingMetricTags.MiniBusEndpoint, context.Options.EndpointName);
        AddIfNotEmpty(ref tags, MiniBusProcessingMetricTags.MiniBusMessageType, GetMessageType(context));
        return tags;
    }

    private static string? GetMessageType(MiniBusProcessingContext context)
    {
        if (context.MessageType?.FullName is { Length: > 0 } messageType)
        {
            return messageType;
        }

        if (context.Headers.TryGetValue(MiniBusHeaderNames.MessageType, out var headerMessageType)
            && !string.IsNullOrWhiteSpace(headerMessageType))
        {
            return headerMessageType;
        }

        return string.IsNullOrWhiteSpace(context.Message.Subject)
            ? null
            : context.Message.Subject;
    }

    private static void AddIfNotEmpty(
        ref TagList tags,
        string key,
        string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            tags.Add(key, value);
        }
    }

    private static double GetElapsedSeconds(long startTimestamp)
    {
        return Stopwatch.GetElapsedTime(startTimestamp).TotalSeconds;
    }

    private static string GetMeterVersion()
    {
        try
        {
            var assembly = typeof(MiniBusProcessingMetrics).Assembly;
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
}
