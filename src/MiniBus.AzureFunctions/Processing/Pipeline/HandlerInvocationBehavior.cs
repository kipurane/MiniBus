using MiniBus.Core.Handlers;

namespace MiniBus.AzureFunctions.Processing.Pipeline;

internal sealed class HandlerInvocationBehavior : IMiniBusProcessingBehavior
{
    private readonly MessageHandlerInvoker _handlerInvoker;
    private readonly MiniBusProcessingLogger _processingLogger;
    private readonly MiniBusProcessingTracer _processingTracer;
    private readonly IServiceProvider _serviceProvider;

    public HandlerInvocationBehavior(
        MessageHandlerInvoker handlerInvoker,
        MiniBusProcessingLogger processingLogger,
        MiniBusProcessingTracer processingTracer,
        IServiceProvider serviceProvider)
    {
        _handlerInvoker = handlerInvoker;
        _processingLogger = processingLogger;
        _processingTracer = processingTracer;
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

        var logHandlerInvoked = _processingLogger.IsHandlerInvocationEnabled();
        var traceHandlerInvoked = context.ProcessingActivity is not null;
        Action<Type>? handlerInvoked = logHandlerInvoked || traceHandlerInvoked
            ? handlerType =>
            {
                if (logHandlerInvoked)
                {
                    _processingLogger.HandlerInvoked(context, handlerType);
                }

                if (traceHandlerInvoked)
                {
                    _processingTracer.HandlerInvoked(context, handlerType);
                }
            }
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
