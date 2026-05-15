using Azure.Messaging.ServiceBus;
using MiniBus.Core.ClaimCheck;
using MiniBus.Core.Serialization;

namespace MiniBus.AzureServiceBus.TransportMessageMapping;

public sealed class AzureServiceBusMessageFactory
{
    private readonly IMessageSerializer _serializer;
    private readonly MiniBusClaimCheckMessageTransformer _transformer;

    public AzureServiceBusMessageFactory(
        IMessageSerializer serializer,
        MiniBusClaimCheckOptions? claimCheckOptions = null,
        IMiniBusClaimCheckPayloadStore? claimCheckPayloadStore = null)
    {
        _serializer = serializer;
        _transformer = new MiniBusClaimCheckMessageTransformer(
            serializer,
            claimCheckOptions,
            claimCheckPayloadStore);
    }

    public ServiceBusMessage CreateMessage(
        object message,
        Type messageType,
        IReadOnlyDictionary<string, string>? headers = null)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(messageType);

        if (!messageType.IsInstanceOfType(message))
        {
            throw new ArgumentException($"Message instance must be assignable to '{messageType.FullName}'.", nameof(message));
        }

        var mappedHeaders = CreateHeaders(messageType, headers);
        var serviceBusMessage = new ServiceBusMessage(_serializer.Serialize(message, messageType));

        AzureServiceBusHeaderMapper.ApplyHeaders(serviceBusMessage, mappedHeaders);
        ApplySystemProperties(serviceBusMessage, mappedHeaders);

        return serviceBusMessage;
    }

    public async Task<ServiceBusMessage> CreateMessageAsync(
        object message,
        Type messageType,
        IReadOnlyDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var outgoingMessage = await _transformer
            .TransformAsync(message, messageType, headers, cancellationToken)
            .ConfigureAwait(false);
        var serviceBusMessage = new ServiceBusMessage(outgoingMessage.Body);

        AzureServiceBusHeaderMapper.ApplyHeaders(serviceBusMessage, outgoingMessage.Headers);
        ApplySystemProperties(serviceBusMessage, outgoingMessage.Headers);

        return serviceBusMessage;
    }

    public ServiceBusMessage CreateMessage(
        BinaryData body,
        Type messageType,
        IReadOnlyDictionary<string, string>? headers = null)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(messageType);

        var mappedHeaders = CreateHeaders(messageType, headers);
        var serviceBusMessage = new ServiceBusMessage(body);

        AzureServiceBusHeaderMapper.ApplyHeaders(serviceBusMessage, mappedHeaders);
        ApplySystemProperties(serviceBusMessage, mappedHeaders);

        return serviceBusMessage;
    }

    private static Dictionary<string, string> CreateHeaders(
        Type messageType,
        IReadOnlyDictionary<string, string>? headers)
    {
        return MiniBusClaimCheckMessageTransformer.CreateHeaders(messageType, headers);
    }

    private static void ApplySystemProperties(ServiceBusMessage message, IReadOnlyDictionary<string, string> headers)
    {
        if (headers.TryGetValue(MiniBusHeaderNames.MessageId, out var messageId))
        {
            message.MessageId = messageId;
        }

        if (headers.TryGetValue(MiniBusHeaderNames.CorrelationId, out var correlationId))
        {
            message.CorrelationId = correlationId;
        }

        if (headers.TryGetValue(MiniBusHeaderNames.ContentType, out var contentType))
        {
            message.ContentType = contentType;
        }

        if (headers.TryGetValue(MiniBusHeaderNames.MessageType, out var messageType))
        {
            message.Subject = messageType;
        }
    }
}
