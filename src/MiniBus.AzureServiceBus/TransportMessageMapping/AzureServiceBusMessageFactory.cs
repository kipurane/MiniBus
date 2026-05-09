using Azure.Messaging.ServiceBus;
using MiniBus.AzureServiceBus.TransportMessageMapping;
using MiniBus.Core.Serialization;

namespace MiniBus.AzureServiceBus.TransportMessageMapping;

public sealed class AzureServiceBusMessageFactory
{
    public const string DefaultContentType = "application/json";

    private readonly IMessageSerializer _serializer;

    public AzureServiceBusMessageFactory(IMessageSerializer serializer)
    {
        _serializer = serializer;
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
        var mappedHeaders = headers is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(headers, StringComparer.Ordinal);

        var messageTypeName = messageType.AssemblyQualifiedName ?? messageType.FullName ?? messageType.Name;
        mappedHeaders.TryAdd(MiniBusHeaderNames.MessageType, messageTypeName);
        mappedHeaders.TryAdd(MiniBusHeaderNames.EnclosedMessageTypes, messageTypeName);
        mappedHeaders.TryAdd(MiniBusHeaderNames.MessageId, Guid.NewGuid().ToString("N"));
        mappedHeaders.TryAdd(MiniBusHeaderNames.ContentType, DefaultContentType);

        return mappedHeaders;
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
