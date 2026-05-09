namespace MiniBus.Core.Persistence;

public interface IMiniBusInbox
{
    Task<bool> IsProcessedAsync(
        MiniBusInboxMessage message,
        CancellationToken cancellationToken = default);
}
