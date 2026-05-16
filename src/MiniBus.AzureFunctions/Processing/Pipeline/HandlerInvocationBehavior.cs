using MiniBus.Core.Handlers;

namespace MiniBus.AzureFunctions.Processing.Pipeline;

internal sealed class HandlerInvocationBehavior : IMiniBusProcessingBehavior
{
    private readonly MessageHandlerInvoker _handlerInvoker;
    private readonly MiniBusProcessingLogger _processingLogger;
    private readonly IServiceProvider _serviceProvider;

    public HandlerInvocationBehavior(
        MessageHandlerInvoker handlerInvoker,
        MiniBusProcessingLogger processingLogger,
        IServiceProvider serviceProvider)
    {
        _handlerInvoker = handlerInvoker;
        _processingLogger = processingLogger;
        _serviceProvider = serviceProvider;
    }

    public async Task InvokeAsync(
        MiniBusProcessingContext context,
        MiniBusProcessingDelegate next,
        CancellationToken cancellationToken)
    {
        if (context.DeserializedMessage is null)
        {
            throw new InvalidOperationException("MiniBus message must be deserialized before handler invocation.");
        }

        if (context.HandlerContext is null)
        {
            throw new InvalidOperationException("MiniBus handler context must be created before handler invocation.");
        }

        Action<Type>? handlerInvoked = _processingLogger.IsHandlerInvocationEnabled()
            ? handlerType => _processingLogger.HandlerInvoked(context, handlerType)
            : null;

        await _handlerInvoker
            .InvokeAsync(
                context.DeserializedMessage,
                context.HandlerContext,
                _serviceProvider,
                cancellationToken,
                handlerInvoked)
            .ConfigureAwait(false);

        await next(context, cancellationToken).ConfigureAwait(false);
    }
}
