using MiniBus.AzureServiceBus.TransportMessageMapping;

namespace MiniBus.AzureFunctions.Processing.Pipeline;

internal sealed class MessageTypeResolutionBehavior : IMiniBusProcessingBehavior
{
    public Task InvokeAsync(
        MiniBusProcessingContext context,
        MiniBusProcessingDelegate next,
        CancellationToken cancellationToken)
    {
        context.MessageType = ResolveMessageType(context.Headers);
        return next(context, cancellationToken);
    }

    public static Type ResolveMessageType(IReadOnlyDictionary<string, string> headers)
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
}
