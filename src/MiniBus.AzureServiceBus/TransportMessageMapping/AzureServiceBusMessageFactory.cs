using Azure.Messaging.ServiceBus;
using MiniBus.Core.ClaimCheck;
using MiniBus.Core.Serialization;
using MiniBus.Core.Identity;

namespace MiniBus.AzureServiceBus.TransportMessageMapping;

public sealed class AzureServiceBusMessageFactory
{
    private readonly MiniBusClaimCheckMessageTransformer _transformer;

    public AzureServiceBusMessageFactory(
        IMessageSerializer serializer,
        MiniBusClaimCheckOptions? claimCheckOptions = null,
        IMiniBusClaimCheckPayloadStore? claimCheckPayloadStore = null,
        MiniBusMessageIdentityRegistry? messageIdentities = null)
    {
        _transformer = new MiniBusClaimCheckMessageTransformer(
            serializer,
            claimCheckOptions,
            claimCheckPayloadStore,
            messageIdentities);
    }

    public ServiceBusMessage CreateMessage(
        object message,
        Type messageType,
        IReadOnlyDictionary<string, string>? headers = null)
    {
        return CreateEnvelope(_transformer.Transform(message, messageType, headers));
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
        return CreateEnvelope(outgoingMessage);
    }

    public ServiceBusMessage CreateMessage(
        BinaryData body,
        Type messageType,
        IReadOnlyDictionary<string, string>? headers = null)
    {
        return CreateEnvelope(_transformer.PrepareStoredBody(body, messageType, headers));
    }

    private static ServiceBusMessage CreateEnvelope(MiniBusOutgoingMessage outgoingMessage)
    {
        var message = new ServiceBusMessage(outgoingMessage.Body);
        AzureServiceBusHeaderMapper.ApplyHeaders(message, outgoingMessage.Headers);
        ApplySystemProperties(message, outgoingMessage.Headers);
        return message;
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
