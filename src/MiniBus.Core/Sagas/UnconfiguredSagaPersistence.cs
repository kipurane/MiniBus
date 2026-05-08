namespace MiniBus.Core.Sagas;

public sealed class UnconfiguredSagaPersistence : ISagaPersistence
{
    private const string Message = "MiniBus saga persistence is not configured. Register an ISagaPersistence implementation for production use, or InMemorySagaPersistence for tests and samples.";

    public Task<SagaPersistenceRecord<TData>?> LoadAsync<TData>(
        string correlationId,
        CancellationToken cancellationToken = default)
        where TData : class, ISagaData, new()
    {
        throw new SagaPersistenceException(Message);
    }

    public Task CreateAsync<TData>(
        TData data,
        CancellationToken cancellationToken = default)
        where TData : class, ISagaData, new()
    {
        throw new SagaPersistenceException(Message);
    }

    public Task SaveAsync<TData>(
        TData data,
        string? version,
        CancellationToken cancellationToken = default)
        where TData : class, ISagaData, new()
    {
        throw new SagaPersistenceException(Message);
    }

    public Task CompleteAsync<TData>(
        TData data,
        string? version,
        CancellationToken cancellationToken = default)
        where TData : class, ISagaData, new()
    {
        throw new SagaPersistenceException(Message);
    }
}
