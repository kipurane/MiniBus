using MiniBus.Core.Persistence;

namespace MiniBus.Persistence.Sql;

public interface ISqlMiniBusOutboxStore
{
    Task<IReadOnlyList<MiniBusOutboxStoredOperation>> ClaimPendingAsync(
        int batchSize,
        CancellationToken cancellationToken = default);

    Task MarkDispatchedAsync(
        Guid operationId,
        CancellationToken cancellationToken = default);

    Task MarkFailedAsync(
        Guid operationId,
        Exception exception,
        CancellationToken cancellationToken = default);

    Task<int> CleanupAsync(
        CancellationToken cancellationToken = default);
}
