using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using MiniBus.AzureFunctions.Processing.Pipeline;
using MiniBus.AzureFunctions.Settlement;
using MiniBus.Core.Auditing;
using MiniBus.Core.Handlers;
using MiniBus.Core.Persistence;
using MiniBus.Core.Recoverability;
using MiniBus.Core.Sagas;
using MiniBus.Core.Serialization;

namespace MiniBus.AzureFunctions.Processing;

public sealed class MiniBusProcessor
{
    public const string DeadLetterReason = "MiniBus processing failed";

    private static readonly ReceivedMessageHeadersBehavior ReceivedMessageHeadersBehavior = new();

    private readonly MiniBusProcessorOptions _options;
    private readonly IServiceProvider _serviceProvider;
    private readonly RecoverabilityDecisionMaker _recoverabilityDecisionMaker;
    private readonly MiniBusProcessingPipeline _pipeline;
    private readonly DelayedRetryScheduler _delayedRetryScheduler;

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
        _pipeline = CreatePipeline(serializer, handlerInvoker, serviceProvider, sagaInvoker);
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
        await ReceivedMessageHeadersBehavior
            .InvokeAsync(context, (_, _) => Task.CompletedTask, cancellationToken)
            .ConfigureAwait(false);

        // Immediate retries stay in this invocation; delayed retry, dead-letter, propagate, and success paths return or throw.
        while (true)
        {
            try
            {
                await _pipeline.InvokeAsync(context, cancellationToken).ConfigureAwait(false);

                context.SettlementDecision = MiniBusSettlementDecision.Complete();
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
            catch (Exception exception) when (exception is not MiniBusPersistenceCommitException
                                             && exception is not MiniBusAuditWriteException
                                             && (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested))
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
                        context = new MiniBusProcessingContext(message, _options, actions)
                        {
                            Headers = decision.Headers,
                            RecoverabilityDecision = decision
                        };
                        continue;
                    case RecoverabilityDecisionKind.DelayedRetry:
                        context.SettlementDecision = MiniBusSettlementDecision.DelayedRetry();
                        await _delayedRetryScheduler.ScheduleAsync(context, decision, cancellationToken)
                            .ConfigureAwait(false);
                        context.SettlementDecision = MiniBusSettlementDecision.Complete();
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
                        throw;
                }
            }
        }
    }

    private async Task ProcessWithoutSettlementAsync(
        MiniBusProcessingContext context,
        CancellationToken cancellationToken)
    {
        await _pipeline.InvokeAsync(context, cancellationToken).ConfigureAwait(false);
        await AuditAsync(
                context,
                context.IsShortCircuited
                    ? MiniBusAuditProcessingOutcome.SkippedDuplicate
                    : MiniBusAuditProcessingOutcome.Completed,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static MiniBusProcessingPipeline CreatePipeline(
        IMessageSerializer serializer,
        MessageHandlerInvoker handlerInvoker,
        IServiceProvider serviceProvider,
        SagaInvoker? sagaInvoker)
    {
        return new MiniBusProcessingPipeline(new IMiniBusProcessingBehavior[]
        {
            new ReceivedMessageHeadersBehavior(),
            new PersistenceBehavior(serviceProvider),
            new MessageTypeResolutionBehavior(),
            new ClaimCheckPayloadResolutionBehavior(serviceProvider),
            new MessageDeserializationBehavior(serializer),
            new HandlerContextBehavior(serviceProvider),
            new HandlerInvocationBehavior(handlerInvoker, serviceProvider),
            new SagaInvocationBehavior(serviceProvider, sagaInvoker)
        });
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
                        context.SettlementDecision.DeadLetterReason ?? RecoverabilityDecisionMaker.RetriesExhaustedDeadLetterReason,
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
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            throw new MiniBusAuditWriteException("MiniBus audit write failed.", exception);
        }
    }
}
