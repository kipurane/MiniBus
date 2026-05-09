namespace MiniBus.Core.Persistence;

public interface IMiniBusOutboxDispatcher
{
    Task DispatchAsync(
        MiniBusOutboxStoredOperation operation,
        CancellationToken cancellationToken = default);
}
