using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using MiniBus.AzureFunctions.Settlement;
using MiniBus.AzureServiceBus.TransportMessageMapping;
using MiniBus.Core.Handlers;
using MiniBus.Core.Serialization;

namespace MiniBus.AzureFunctions.Processing;

public sealed class MiniBusProcessor
{
    public const string DeadLetterReason = "MiniBus processing failed";

    private readonly IMessageSerializer _serializer;
    private readonly MessageHandlerInvoker _handlerInvoker;
    private readonly IServiceProvider _serviceProvider;
    private readonly MiniBusProcessorOptions _options;

    public MiniBusProcessor(
        IMessageSerializer serializer,
        MessageHandlerInvoker handlerInvoker,
        IServiceProvider serviceProvider,
        MiniBusProcessorOptions? options = null)
    {
        _serializer = serializer;
        _handlerInvoker = handlerInvoker;
        _serviceProvider = serviceProvider;
        _options = options ?? new MiniBusProcessorOptions();
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

        try
        {
            await ProcessCoreAsync(message, cancellationToken).ConfigureAwait(false);

            await actions.CompleteMessageAsync(message, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            await actions
                .DeadLetterMessageAsync(message, DeadLetterReason, exception.Message, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private async Task ProcessCoreAsync(
        ServiceBusReceivedMessage message,
        CancellationToken cancellationToken)
    {
        var headers = AzureServiceBusHeaderMapper.ReadHeaders(message);
        var messageType = ResolveMessageType(headers);
        var deserializedMessage = _serializer.Deserialize(message.Body, messageType);
        var context = CreateContext(message, headers);

        await _handlerInvoker
            .InvokeAsync(deserializedMessage, context, _serviceProvider, cancellationToken)
            .ConfigureAwait(false);
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
