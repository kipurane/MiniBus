using MiniBus.AzureServiceBus.TransportMessageMapping;
using MiniBus.Core.Identity;

namespace MiniBus.AzureFunctions.Processing.Pipeline;

internal sealed class MessageTypeResolutionBehavior : IMiniBusProcessingBehavior
{
    private readonly MiniBusMessageIdentityRegistry _messageIdentities;

    public MessageTypeResolutionBehavior(MiniBusMessageIdentityRegistry? messageIdentities = null)
    {
        _messageIdentities = messageIdentities ?? new MiniBusMessageIdentityRegistry();
    }

    public Task InvokeAsync(
        MiniBusProcessingContext context,
        MiniBusProcessingDelegate next,
        CancellationToken cancellationToken)
    {
        context.MessageType = ResolveMessageType(context.Headers, _messageIdentities);
        return next(context, cancellationToken);
    }

    public static Type ResolveMessageType(
        IReadOnlyDictionary<string, string> headers,
        MiniBusMessageIdentityRegistry? messageIdentities = null)
    {
        var messageTypeName = GetMessageTypeName(headers);
        try
        {
            return (messageIdentities ?? new MiniBusMessageIdentityRegistry()).Resolve(messageTypeName);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            throw new MiniBusMessageTypeResolutionException(
                $"MiniBus message type '{messageTypeName}' could not be resolved.", exception);
        }
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
}
