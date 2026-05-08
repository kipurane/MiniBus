using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using MiniBus.AzureServiceBus.Recoverability;
using MiniBus.AzureFunctions.Settlement;
using MiniBus.AzureServiceBus.TransportMessageMapping;
using MiniBus.Core.Handlers;
using MiniBus.Core.Recoverability;
using MiniBus.Core.Sagas;
using MiniBus.Core.Serialization;

namespace MiniBus.AzureFunctions.Processing;

public sealed class MiniBusProcessor
{
    public const string DeadLetterReason = "MiniBus processing failed";

    private readonly IMessageSerializer _serializer;
    private readonly MessageHandlerInvoker _handlerInvoker;
    private readonly IServiceProvider _serviceProvider;
    private readonly MiniBusProcessorOptions _options;
    private readonly RecoverabilityDecisionMaker _recoverabilityDecisionMaker;
    private readonly SagaInvoker? _sagaInvoker;

    public MiniBusProcessor(
        IMessageSerializer serializer,
        MessageHandlerInvoker handlerInvoker,
        IServiceProvider serviceProvider,
        MiniBusProcessorOptions? options = null,
        RecoverabilityDecisionMaker? recoverabilityDecisionMaker = null,
        SagaInvoker? sagaInvoker = null)
    {
        _serializer = serializer;
        _handlerInvoker = handlerInvoker;
        _serviceProvider = serviceProvider;
        _options = options ?? new MiniBusProcessorOptions();
        _recoverabilityDecisionMaker = recoverabilityDecisionMaker ?? new RecoverabilityDecisionMaker();
        _sagaInvoker = sagaInvoker;
    }

    public Task ProcessAsync(
        ServiceBusReceivedMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        return ProcessCoreAsync(message, cancellationToken);
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

        var headers = CreateReceivedHeaders(message);

        // Immediate retries stay in this invocation; delayed retry, dead-letter, propagate, and success paths return or throw.
        while (true)
        {
            try
            {
                await ProcessCoreAsync(message, headers, cancellationToken).ConfigureAwait(false);

                await actions.CompleteMessageAsync(message, cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                var decision = _recoverabilityDecisionMaker.Decide(
                    headers,
                    _options.Recoverability,
                    exception,
                    message.MessageId);

                headers = decision.Headers;

                switch (decision.Kind)
                {
                    case RecoverabilityDecisionKind.ImmediateRetry:
                        continue;
                    case RecoverabilityDecisionKind.DelayedRetry:
                        await ScheduleDelayedRetryAsync(message, headers, decision, cancellationToken).ConfigureAwait(false);
                        await actions.CompleteMessageAsync(message, cancellationToken).ConfigureAwait(false);
                        return;
                    case RecoverabilityDecisionKind.DeadLetter:
                        await actions
                            .DeadLetterMessageAsync(
                                message,
                                decision.DeadLetterReason ?? RecoverabilityDecisionMaker.RetriesExhaustedDeadLetterReason,
                                decision.DeadLetterDescription,
                                cancellationToken)
                            .ConfigureAwait(false);
                        return;
                    case RecoverabilityDecisionKind.Propagate:
                    default:
                        throw;
                }
            }
        }
    }

    private async Task ProcessCoreAsync(
        ServiceBusReceivedMessage message,
        CancellationToken cancellationToken)
    {
        var headers = CreateReceivedHeaders(message);
        await ProcessCoreAsync(message, headers, cancellationToken).ConfigureAwait(false);
    }

    private async Task ProcessCoreAsync(
        ServiceBusReceivedMessage message,
        IReadOnlyDictionary<string, string> headers,
        CancellationToken cancellationToken)
    {
        var messageType = ResolveMessageType(headers);
        var deserializedMessage = _serializer.Deserialize(message.Body, messageType);
        var context = CreateContext(message, headers);

        await _handlerInvoker
            .InvokeAsync(deserializedMessage, context, _serviceProvider, cancellationToken)
            .ConfigureAwait(false);

        if (!_options.EnableSagas)
        {
            return;
        }

        if (_sagaInvoker is null)
        {
            throw new InvalidOperationException("MiniBus saga processing is enabled, but SagaInvoker is not configured. Register SagaRegistry, ISagaPersistence, and SagaInvoker, or set MiniBusProcessorOptions.EnableSagas to false.");
        }

        await _sagaInvoker
            .InvokeAsync(deserializedMessage, context, _serviceProvider, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task ScheduleDelayedRetryAsync(
        ServiceBusReceivedMessage message,
        IReadOnlyDictionary<string, string> headers,
        RecoverabilityDecision decision,
        CancellationToken cancellationToken)
    {
        var scheduler = _serviceProvider.GetService(typeof(IAzureServiceBusDelayedRetryScheduler)) as IAzureServiceBusDelayedRetryScheduler
                        ?? throw new InvalidOperationException("Azure Service Bus delayed retry scheduling is not configured for MiniBus Azure Functions processing. Register MiniBus Azure Functions with AddMiniBusAzureFunctions, or register IAzureServiceBusDelayedRetryScheduler with AzureServiceBusDelayedRetryScheduler.");

        var messageType = ResolveMessageType(headers);
        var dueTime = DateTimeOffset.UtcNow.Add(decision.Delay ?? TimeSpan.Zero);

        await scheduler
            .ScheduleRetryAsync(message, messageType, dueTime, headers, cancellationToken)
            .ConfigureAwait(false);
    }

    private static IReadOnlyDictionary<string, string> CreateReceivedHeaders(ServiceBusReceivedMessage message)
    {
        var headers = new Dictionary<string, string>(AzureServiceBusHeaderMapper.ReadHeaders(message), StringComparer.Ordinal);

        if (!string.IsNullOrWhiteSpace(message.MessageId))
        {
            headers.TryAdd(MiniBusHeaderNames.MessageId, message.MessageId);
        }

        if (!string.IsNullOrWhiteSpace(message.CorrelationId))
        {
            headers.TryAdd(MiniBusHeaderNames.CorrelationId, message.CorrelationId);
        }

        if (!string.IsNullOrWhiteSpace(message.ContentType))
        {
            headers.TryAdd(MiniBusHeaderNames.ContentType, message.ContentType);
        }

        if (!string.IsNullOrWhiteSpace(message.Subject))
        {
            headers.TryAdd(MiniBusHeaderNames.MessageType, message.Subject);
        }

        return headers;
    }

    private static Type ResolveMessageType(IReadOnlyDictionary<string, string> headers)
    {
        var messageTypeName = GetMessageTypeName(headers);
        var messageType = Type.GetType(messageTypeName, throwOnError: false);

        if (messageType is null)
        {
            throw new MiniBusMessageTypeResolutionException($"MiniBus message type '{messageTypeName}' could not be resolved.");
        }

        return messageType;
    }

    private static string GetMessageTypeName(IReadOnlyDictionary<string, string> headers)
    {
        if (headers.TryGetValue(MiniBusHeaderNames.MessageType, out var messageType)
            && !string.IsNullOrWhiteSpace(messageType))
        {
            return messageType;
        }

        if (headers.TryGetValue(MiniBusHeaderNames.EnclosedMessageTypes, out var enclosedMessageTypes)
            && !string.IsNullOrWhiteSpace(enclosedMessageTypes))
        {
            var firstMessageType = enclosedMessageTypes
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(firstMessageType))
            {
                return firstMessageType;
            }
        }

        throw new MiniBusMessageTypeResolutionException("MiniBus message type metadata is missing.");
    }

    private MiniBusReceivedMessageContext CreateContext(
        ServiceBusReceivedMessage message,
        IReadOnlyDictionary<string, string> headers)
    {
        var messageId = GetHeaderOrValue(headers, MiniBusHeaderNames.MessageId, message.MessageId);
        var correlationId = GetHeaderOrValue(headers, MiniBusHeaderNames.CorrelationId, message.CorrelationId);
        var causationId = headers.TryGetValue(MiniBusHeaderNames.CausationId, out var headerCausationId)
            ? headerCausationId
            : null;

        return new MiniBusReceivedMessageContext(
            _options.EndpointName,
            messageId,
            correlationId,
            causationId,
            headers,
            _serviceProvider);
    }

    private static string GetHeaderOrValue(
        IReadOnlyDictionary<string, string> headers,
        string headerName,
        string? fallback)
    {
        if (headers.TryGetValue(headerName, out var headerValue) && !string.IsNullOrWhiteSpace(headerValue))
        {
            return headerValue;
        }

        if (!string.IsNullOrWhiteSpace(fallback))
        {
            return fallback;
        }

        return Guid.NewGuid().ToString("N");
    }
}
