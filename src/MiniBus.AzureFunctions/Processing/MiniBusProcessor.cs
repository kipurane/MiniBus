using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MiniBus.AzureFunctions.Processing.Pipeline;
using MiniBus.AzureFunctions.Settlement;
using MiniBus.Core.Auditing;
using MiniBus.Core.Handlers;
using MiniBus.Core.Persistence;
using MiniBus.Core.Recoverability;
using MiniBus.Core.Sagas;
using MiniBus.Core.Serialization;
using System.Runtime.ExceptionServices;

namespace MiniBus.AzureFunctions.Processing;

public sealed class MiniBusProcessor
{
    public const string DeadLetterReason = "MiniBus processing failed";

    private static readonly ReceivedMessageHeadersBehavior ReceivedMessageHeadersBehavior = new();
    private static readonly MiniBusProcessingDelegate NoopProcessingDelegate = static (_, _) => Task.CompletedTask;

    private readonly MiniBusProcessorOptions _options;
    private readonly IServiceProvider _serviceProvider;
    private readonly RecoverabilityDecisionMaker _recoverabilityDecisionMaker;
    private readonly MiniBusProcessingPipeline _pipeline;
    private readonly DelayedRetryScheduler _delayedRetryScheduler;
    private readonly MiniBusProcessingLogger _processingLogger;
    private readonly MiniBusProcessingTracer _processingTracer;
    private readonly MiniBusProcessingMetrics _processingMetrics;

    public MiniBusProcessor(
        IMessageSerializer serializer,
        MessageHandlerInvoker handlerInvoker,
        IServiceProvider serviceProvider,
        MiniBusProcessorOptions? options = null,
        RecoverabilityDecisionMaker? recoverabilityDecisionMaker = null,
        SagaInvoker? sagaInvoker = null)
    {
        ArgumentNullException.ThrowIfNull(serializer);
        ArgumentNullException.ThrowIfNull(handlerInvoker);
        ArgumentNullException.ThrowIfNull(serviceProvider);

        _options = options ?? new MiniBusProcessorOptions();
        _serviceProvider = serviceProvider;
        _recoverabilityDecisionMaker = recoverabilityDecisionMaker ?? new RecoverabilityDecisionMaker();
        _processingLogger = new MiniBusProcessingLogger(serviceProvider.GetService<ILoggerFactory>());
        _processingTracer = new MiniBusProcessingTracer();
        _processingMetrics = new MiniBusProcessingMetrics();
        _pipeline = CreatePipeline(
            serializer,
            handlerInvoker,
            serviceProvider,
            _processingLogger,
            _processingTracer,
            _processingMetrics,
            sagaInvoker);
        _delayedRetryScheduler = new DelayedRetryScheduler(serviceProvider);
    }

    public Task ProcessAsync(
        ServiceBusReceivedMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        var context = new MiniBusProcessingContext(message, _options);
        return ProcessWithoutSettlementAsync(context, cancellationToken);
    }

    public Task ProcessAsync(
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions actions,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actions);
        return ProcessAsync(message, new ServiceBusMessageActionsAdapter(actions), cancellationToken);
    }

    public async Task ProcessAsync(
        ServiceBusReceivedMessage message,
        IMiniBusMessageActions actions,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(actions);

        var context = new MiniBusProcessingContext(message, _options, actions);
        await LoadReceivedMessageHeadersAsync(context, cancellationToken).ConfigureAwait(false);

        // Immediate retries stay in this invocation; delayed retry, dead-letter, propagate, and success paths return or throw.
        while (true)
        {
            using var processingScope = BeginProcessingAttempt(context);
            try
            {
                await _pipeline.InvokeAsync(context, cancellationToken).ConfigureAwait(false);

                context.SettlementDecision = MiniBusSettlementDecision.Complete();
                LogTerminalSuccess(context);
                await AuditAsync(
                        context,
                        context.IsShortCircuited
                            ? MiniBusAuditProcessingOutcome.SkippedDuplicate
                            : MiniBusAuditProcessingOutcome.Completed,
                        cancellationToken)
                    .ConfigureAwait(false);
                await ApplySettlementAsync(context, cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (Exception exception) when (IsRecoverableProcessingException(exception, cancellationToken))
            {
                var decision = _recoverabilityDecisionMaker.Decide(
                    context.Headers,
                    _options.Recoverability,
                    exception,
                    message.MessageId);

                context.Headers = decision.Headers;
                context.RecoverabilityDecision = decision;

                switch (decision.Kind)
                {
                    case RecoverabilityDecisionKind.ImmediateRetry:
                        _processingLogger.ProcessingRetried(context, exception);
                        _processingTracer.ProcessingRetried(context, exception);
                        _processingMetrics.ProcessingRetried(context);
                        context = new MiniBusProcessingContext(message, _options, actions)
                        {
                            Headers = decision.Headers,
                            RecoverabilityDecision = decision
                        };
                        continue;
                    case RecoverabilityDecisionKind.DelayedRetry:
                        context.SettlementDecision = MiniBusSettlementDecision.DelayedRetry();
                        try
                        {
                            await _delayedRetryScheduler.ScheduleAsync(context, decision, cancellationToken)
                                .ConfigureAwait(false);
                        }
                        catch (Exception ex) when (IsNotCallerRequestedCancellation(ex, cancellationToken))
                        {
                            _processingLogger.ProcessingFailed(context, ex);
                            _processingTracer.ProcessingFailed(context, ex);
                            _processingMetrics.ProcessingFailed(context);
                            throw;
                        }

                        context.SettlementDecision = MiniBusSettlementDecision.Complete();
                        _processingLogger.ProcessingDelayedRetryScheduled(context);
                        _processingTracer.ProcessingDelayedRetryScheduled(context);
                        _processingMetrics.ProcessingDelayedRetryScheduled(context);
                        await AuditAsync(
                                context,
                                MiniBusAuditProcessingOutcome.DelayedRetryScheduled,
                                cancellationToken)
                            .ConfigureAwait(false);
                        await ApplySettlementAsync(context, cancellationToken).ConfigureAwait(false);
                        return;
                    case RecoverabilityDecisionKind.DeadLetter:
                        context.SettlementDecision = MiniBusSettlementDecision.DeadLetter(
                            decision.DeadLetterReason ?? RecoverabilityDecisionMaker.RetriesExhaustedDeadLetterReason,
                            decision.DeadLetterDescription);
                        _processingLogger.ProcessingDeadLettered(
                            context,
                            context.SettlementDecision.DeadLetterReason,
                            context.SettlementDecision.DeadLetterDescription);
                        _processingTracer.ProcessingDeadLettered(
                            context,
                            context.SettlementDecision.DeadLetterReason,
                            context.SettlementDecision.DeadLetterDescription);
                        _processingMetrics.ProcessingDeadLettered(context);
                        await AuditAsync(
                                context,
                                MiniBusAuditProcessingOutcome.DeadLettered,
                                cancellationToken,
                                context.SettlementDecision.DeadLetterReason,
                                context.SettlementDecision.DeadLetterDescription)
                            .ConfigureAwait(false);
                        await ApplySettlementAsync(context, cancellationToken).ConfigureAwait(false);
                        return;
                    case RecoverabilityDecisionKind.Propagate:
                    default:
                        _processingLogger.ProcessingFailed(context, exception);
                        _processingTracer.ProcessingFailed(context, exception);
                        _processingMetrics.ProcessingFailed(context);
                        throw;
                }
            }
            // Exceptions rethrown from the recoverability catch above do not flow into sibling catches on this try.
            // This catch therefore only handles failures from the main processing body that were not already logged.
            catch (Exception exception) when (IsNotCallerRequestedCancellation(exception, cancellationToken))
            {
                _processingLogger.ProcessingFailed(context, exception);
                _processingTracer.ProcessingFailed(context, exception);
                _processingMetrics.ProcessingFailed(context);
                throw;
            }
        }
    }

    private async Task ProcessWithoutSettlementAsync(
        MiniBusProcessingContext context,
        CancellationToken cancellationToken)
    {
        await LoadReceivedMessageHeadersAsync(context, cancellationToken).ConfigureAwait(false);

        using var processingScope = BeginProcessingAttempt(context);
        try
        {
            await _pipeline.InvokeAsync(context, cancellationToken).ConfigureAwait(false);
            LogTerminalSuccess(context);
            await AuditAsync(
                    context,
                    context.IsShortCircuited
                        ? MiniBusAuditProcessingOutcome.SkippedDuplicate
                        : MiniBusAuditProcessingOutcome.Completed,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (IsNotCallerRequestedCancellation(exception, cancellationToken))
        {
            _processingLogger.ProcessingFailed(context, exception);
            _processingTracer.ProcessingFailed(context, exception);
            _processingMetrics.ProcessingFailed(context);
            throw;
        }
    }

    private static MiniBusProcessingPipeline CreatePipeline(
        IMessageSerializer serializer,
        MessageHandlerInvoker handlerInvoker,
        IServiceProvider serviceProvider,
        MiniBusProcessingLogger processingLogger,
        MiniBusProcessingTracer processingTracer,
        MiniBusProcessingMetrics processingMetrics,
        SagaInvoker? sagaInvoker)
    {
        return new MiniBusProcessingPipeline(new IMiniBusProcessingBehavior[]
        {
            new PersistenceBehavior(serviceProvider, processingLogger, processingTracer),
            new MessageTypeResolutionBehavior(),
            new ClaimCheckPayloadResolutionBehavior(serviceProvider),
            new MessageDeserializationBehavior(serializer),
            new HandlerContextBehavior(serviceProvider),
            new HandlerInvocationBehavior(handlerInvoker, processingLogger, processingTracer, processingMetrics,
                serviceProvider),
            new SagaInvocationBehavior(serviceProvider, processingLogger, processingTracer, processingMetrics,
                sagaInvoker)
        });
    }

    private IDisposable BeginProcessingAttempt(MiniBusProcessingContext context)
    {
        var activity = _processingTracer.StartProcessingActivity(context);
        context.ProcessingMetricAttempt = _processingMetrics.StartProcessingAttempt();
        var scope = _processingLogger.BeginProcessingScope(context);
        _processingLogger.ProcessingStarted(context);
        return new ProcessingAttemptScope(scope, activity);
    }

    private static Task LoadReceivedMessageHeadersAsync(
        MiniBusProcessingContext context,
        CancellationToken cancellationToken)
    {
        // Header loading happens before processing begins so the initial scope and start log include message metadata
        // consistently for settlement and no-settlement processing paths.
        return ReceivedMessageHeadersBehavior.InvokeAsync(context, NoopProcessingDelegate, cancellationToken);
    }

    private void LogTerminalSuccess(MiniBusProcessingContext context)
    {
        if (context.IsShortCircuited)
        {
            _processingLogger.ProcessingSkippedDuplicate(context);
            _processingTracer.ProcessingSkippedDuplicate(context);
            _processingMetrics.ProcessingSkippedDuplicate(context);
            return;
        }

        _processingLogger.ProcessingCompleted(context);
        _processingTracer.ProcessingCompleted(context);
        _processingMetrics.ProcessingCompleted(context);
    }

    private static async Task ApplySettlementAsync(
        MiniBusProcessingContext context,
        CancellationToken cancellationToken)
    {
        if (context.Actions is null)
        {
            return;
        }

        switch (context.SettlementDecision.Kind)
        {
            case MiniBusSettlementDecisionKind.Complete:
                await context.Actions.CompleteMessageAsync(context.Message, cancellationToken).ConfigureAwait(false);
                return;
            case MiniBusSettlementDecisionKind.DeadLetter:
                await context.Actions
                    .DeadLetterMessageAsync(
                        context.Message,
                        context.SettlementDecision.DeadLetterReason ??
                        RecoverabilityDecisionMaker.RetriesExhaustedDeadLetterReason,
                        context.SettlementDecision.DeadLetterDescription,
                        cancellationToken)
                    .ConfigureAwait(false);
                return;
            case MiniBusSettlementDecisionKind.None:
            case MiniBusSettlementDecisionKind.DelayedRetry:
            default:
                return;
        }
    }

    private async Task AuditAsync(
        MiniBusProcessingContext context,
        MiniBusAuditProcessingOutcome outcome,
        CancellationToken cancellationToken,
        string? deadLetterReason = null,
        string? deadLetterDescription = null)
    {
        var auditWriter = _serviceProvider.GetService<IMiniBusAuditWriter>();
        if (auditWriter is null)
        {
            return;
        }

        try
        {
            context.Options.Audit.Validate();
            var record = MiniBusAuditRecordFactory.Create(
                context,
                outcome,
                context.Options.Audit.AuditIdFactory(),
                context.Options.Audit.UtcNowProvider(),
                deadLetterReason,
                deadLetterDescription);

            await auditWriter.WriteAsync(record, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (IsNotCallerRequestedCancellation(exception, cancellationToken))
        {
            throw new MiniBusAuditWriteException("MiniBus audit write failed.", exception);
        }
    }

    private static bool IsNotCallerRequestedCancellation(
        Exception exception,
        CancellationToken cancellationToken)
    {
        return exception is not OperationCanceledException
               || !cancellationToken.IsCancellationRequested;
    }

    private static bool IsRecoverableProcessingException(
        Exception exception,
        CancellationToken cancellationToken)
    {
        return exception is not MiniBusPersistenceCommitException
               && exception is not MiniBusAuditWriteException
               && IsNotCallerRequestedCancellation(exception, cancellationToken);
    }

    private sealed class ProcessingAttemptScope : IDisposable
    {
        private readonly IDisposable _loggingScope;
        private readonly IDisposable? _activity;

        public ProcessingAttemptScope(
            IDisposable loggingScope,
            IDisposable? activity)
        {
            _loggingScope = loggingScope;
            _activity = activity;
        }

        public void Dispose()
        {
            // Always attempt both disposals; if both fail, surface both exceptions to avoid hiding either provider failure.
            Exception? loggingScopeException = null;
            try
            {
                _loggingScope.Dispose();
            }
            catch (Exception exception)
            {
                loggingScopeException = exception;
            }

            try
            {
                _activity?.Dispose();
            }
            catch (Exception activityException) when (loggingScopeException is not null)
            {
                throw new AggregateException(
                    "MiniBus processing scope disposal failed.",
                    loggingScopeException,
                    activityException);
            }

            if (loggingScopeException is not null)
            {
                ExceptionDispatchInfo.Capture(loggingScopeException).Throw();
            }
        }
    }
}