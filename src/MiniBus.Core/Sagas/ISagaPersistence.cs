namespace MiniBus.Core.Sagas;

public interface ISagaPersistence
{
    Task<SagaPersistenceRecord<TData>?> LoadAsync<TData>(
        string correlationId,
        CancellationToken cancellationToken = default)
        where TData : class, ISagaData, new();

    Task CreateAsync<TData>(
        TData data,
        CancellationToken cancellationToken = default)
        where TData : class, ISagaData, new();

    Task SaveAsync<TData>(
        TData data,
        string? version,
        CancellationToken cancellationToken = default)
        where TData : class, ISagaData, new();

    Task CompleteAsync<TData>(
        TData data,
        string? version,
        CancellationToken cancellationToken = default)
        where TData : class, ISagaData, new();
}
