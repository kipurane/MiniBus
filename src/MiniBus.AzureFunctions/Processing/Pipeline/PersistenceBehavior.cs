using MiniBus.AzureServiceBus.TransportMessageMapping;
using MiniBus.Core.Persistence;
using MiniBus.Core.Recoverability;

namespace MiniBus.AzureFunctions.Processing.Pipeline;

internal sealed class PersistenceBehavior : IMiniBusProcessingBehavior
{
    private readonly IServiceProvider _serviceProvider;
    private readonly MiniBusProcessingLogger _processingLogger;

    public PersistenceBehavior(
        IServiceProvider serviceProvider,
        MiniBusProcessingLogger processingLogger)
    {
        _serviceProvider = serviceProvider;
        _processingLogger = processingLogger;
    }

    public async Task InvokeAsync(
        MiniBusProcessingContext context,
        MiniBusProcessingDelegate next,
        CancellationToken cancellationToken)
    {
        var persistenceSessionFactory = GetPersistenceSessionFactory();

        if (persistenceSessionFactory is null)
        {
            await next(context, cancellationToken).ConfigureAwait(false);
            return;
        }

        await using var session = await persistenceSessionFactory.CreateAsync(cancellationToken)
            .ConfigureAwait(false);
        context.PersistenceSession = session;
        context.InboxMessage = CreateInboxMessage(context);

        if (context.Options.Persistence.EnableInbox
            && await session.IsProcessedAsync(context.InboxMessage, cancellationToken).ConfigureAwait(false))
        {
            context.ShortCircuit();
            return;
        }

        context.OutboxCollector = context.Options.Persistence.EnableOutbox
            ? new MiniBusOutboxOperationCollector()
            : null;

        await next(context, cancellationToken).ConfigureAwait(false);

        try
        {
            await session
                .CommitAsync(
                    context.InboxMessage,
                    context.OutboxOperations,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (IsNotCallerRequestedCancellation(exception, cancellationToken))
        {
            throw new MiniBusPersistenceCommitException("MiniBus persistence commit failed.", exception);
        }

        _processingLogger.OutboxCommitted(context);
    }

    private IMiniBusPersistenceSessionFactory? GetPersistenceSessionFactory()
    {
        return _serviceProvider.GetService(typeof(IMiniBusPersistenceSessionFactory)) as IMiniBusPersistenceSessionFactory;
    }

    private static bool IsNotCallerRequestedCancellation(
        Exception exception,
        CancellationToken cancellationToken)
    {
        return exception is not OperationCanceledException
               || !cancellationToken.IsCancellationRequested;
    }

    private static MiniBusInboxMessage CreateInboxMessage(MiniBusProcessingContext context)
    {
        return new MiniBusInboxMessage(
            context.Options.EndpointName,
            GetLogicalMessageId(context),
            context.Headers,
            DateTimeOffset.UtcNow);
    }

    private static string GetLogicalMessageId(MiniBusProcessingContext context)
    {
        if (context.Headers.TryGetValue(MiniBusRecoverabilityHeaderNames.OriginalMessageId, out var originalMessageId)
            && !string.IsNullOrWhiteSpace(originalMessageId))
        {
            return originalMessageId;
        }

        return HandlerContextBehavior.GetHeaderOrValue(
            context.Headers,
            MiniBusHeaderNames.MessageId,
            context.Message.MessageId);
    }
}
