namespace MiniBus.Core.Sagas;

public sealed record SagaPersistenceRecord<TData>(
    TData Data,
    string? Version)
    where TData : class, ISagaData, new();
