using MiniBus.AzureServiceBus.TransportMessageMapping;

namespace MiniBus.AzureFunctions.Processing.Pipeline;

internal sealed class HandlerContextBehavior : IMiniBusProcessingBehavior
{
    private readonly IServiceProvider _serviceProvider;

    public HandlerContextBehavior(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public Task InvokeAsync(
        MiniBusProcessingContext context,
        MiniBusProcessingDelegate next,
        CancellationToken cancellationToken)
    {
        var messageId = GetHeaderOrValue(context.Headers, MiniBusHeaderNames.MessageId, context.Message.MessageId);
        var correlationId = GetHeaderOrValue(context.Headers, MiniBusHeaderNames.CorrelationId, context.Message.CorrelationId);
        var causationId = context.Headers.TryGetValue(MiniBusHeaderNames.CausationId, out var headerCausationId)
            ? headerCausationId
            : null;

        context.HandlerContext = new MiniBusReceivedMessageContext(
            context.Options.EndpointName,
            messageId,
            correlationId,
            causationId,
            context.Headers,
            _serviceProvider,
            context.OutboxCollector);

        return next(context, cancellationToken);
    }

    internal static string GetHeaderOrValue(
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
