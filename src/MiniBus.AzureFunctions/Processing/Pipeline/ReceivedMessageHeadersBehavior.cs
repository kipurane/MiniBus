using MiniBus.AzureServiceBus.TransportMessageMapping;

namespace MiniBus.AzureFunctions.Processing.Pipeline;

internal sealed class ReceivedMessageHeadersBehavior : IMiniBusProcessingBehavior
{
    public Task InvokeAsync(
        MiniBusProcessingContext context,
        MiniBusProcessingDelegate next,
        CancellationToken cancellationToken)
    {
        if (context.Headers.Count > 0)
        {
            return next(context, cancellationToken);
        }

        var headers = new Dictionary<string, string>(
            AzureServiceBusHeaderMapper.ReadHeaders(context.Message),
            StringComparer.Ordinal);

        if (!string.IsNullOrWhiteSpace(context.Message.MessageId))
        {
            headers.TryAdd(MiniBusHeaderNames.MessageId, context.Message.MessageId);
        }

        if (!string.IsNullOrWhiteSpace(context.Message.CorrelationId))
        {
            headers.TryAdd(MiniBusHeaderNames.CorrelationId, context.Message.CorrelationId);
        }

        if (!string.IsNullOrWhiteSpace(context.Message.ContentType))
        {
            headers.TryAdd(MiniBusHeaderNames.ContentType, context.Message.ContentType);
        }

        if (!string.IsNullOrWhiteSpace(context.Message.Subject))
        {
            headers.TryAdd(MiniBusHeaderNames.MessageType, context.Message.Subject);
        }

        context.Headers = headers;
        return next(context, cancellationToken);
    }
}
