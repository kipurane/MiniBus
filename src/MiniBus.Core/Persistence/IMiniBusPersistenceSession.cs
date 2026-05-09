namespace MiniBus.Core.Persistence;

public interface IMiniBusPersistenceSession : IAsyncDisposable
{
    Task<bool> IsProcessedAsync(
        MiniBusInboxMessage message,
        CancellationToken cancellationToken = default);

    Task CommitAsync(
        MiniBusInboxMessage message,
        IReadOnlyCollection<MiniBusOutboxOperation> outboxOperations,
        CancellationToken cancellationToken = default);
}
