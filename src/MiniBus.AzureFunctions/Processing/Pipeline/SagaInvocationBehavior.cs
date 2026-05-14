using MiniBus.Core.Sagas;

namespace MiniBus.AzureFunctions.Processing.Pipeline;

internal sealed class SagaInvocationBehavior : IMiniBusProcessingBehavior
{
    private readonly IServiceProvider _serviceProvider;
    private readonly SagaInvoker? _sagaInvoker;

    public SagaInvocationBehavior(
        IServiceProvider serviceProvider,
        SagaInvoker? sagaInvoker)
    {
        _serviceProvider = serviceProvider;
        _sagaInvoker = sagaInvoker;
    }

    public async Task InvokeAsync(
        MiniBusProcessingContext context,
        MiniBusProcessingDelegate next,
        CancellationToken cancellationToken)
    {
        if (!context.Options.EnableSagas)
        {
            await next(context, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (_sagaInvoker is null)
        {
            throw new InvalidOperationException("MiniBus saga processing is enabled, but SagaInvoker is not configured. Register SagaRegistry, ISagaPersistence, and SagaInvoker, or set MiniBusProcessorOptions.EnableSagas to false.");
        }

        if (context.DeserializedMessage is null)
        {
            throw new InvalidOperationException("MiniBus message must be deserialized before saga invocation.");
        }

        if (context.HandlerContext is null)
        {
            throw new InvalidOperationException("MiniBus handler context must be created before saga invocation.");
        }

        await _sagaInvoker
            .InvokeAsync(context.DeserializedMessage, context.HandlerContext, _serviceProvider, cancellationToken)
            .ConfigureAwait(false);

        await next(context, cancellationToken).ConfigureAwait(false);
    }
}
