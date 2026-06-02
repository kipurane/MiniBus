namespace MiniBus.Core.Sagas;

/// <summary>
/// Stores saga data and optimistic-concurrency metadata for long-running workflows.
/// </summary>
public interface ISagaPersistence
{
    /// <summary>
    /// Loads saga data and its required version token for an existing saga correlation id.
    /// </summary>
    /// <remarks>
    /// The returned version is an opaque provider-owned concurrency token. Callers must pass the
    /// token back unchanged to <see cref="SaveAsync{TData}" /> or <see cref="CompleteAsync{TData}" />.
    /// Providers must return a non-empty token for existing saga data.
    /// </remarks>
    Task<SagaPersistenceRecord<TData>?> LoadAsync<TData>(
        string correlationId,
        CancellationToken cancellationToken = default)
        where TData : class, ISagaData, new();

    /// <summary>
    /// Creates saga data exactly as supplied, including completed state when <see cref="ISagaData.IsCompleted" /> is true.
    /// </summary>
    Task CreateAsync<TData>(
        TData data,
        CancellationToken cancellationToken = default)
        where TData : class, ISagaData, new();

    /// <summary>
    /// Saves existing saga data only when the required version token matches the stored saga version.
    /// </summary>
    /// <remarks>
    /// The version is an opaque provider-owned concurrency token returned by <see cref="LoadAsync{TData}" />.
    /// Implementations should reject null, empty, or malformed tokens with <see cref="ArgumentException" />
    /// and should report missing saga rows or stale tokens with <see cref="SagaPersistenceException" />.
    /// Completed saga state is monotonic; implementations should reject attempts to mark a previously
    /// completed saga incomplete.
    /// </remarks>
    Task SaveAsync<TData>(
        TData data,
        string version,
        CancellationToken cancellationToken = default)
        where TData : class, ISagaData, new();

    /// <summary>
    /// Marks existing saga data complete only when the required version token matches the stored saga version.
    /// </summary>
    /// <remarks>
    /// The version is an opaque provider-owned concurrency token returned by <see cref="LoadAsync{TData}" />.
    /// Implementations should reject null, empty, or malformed tokens with <see cref="ArgumentException" />
    /// and should report missing saga rows or stale tokens with <see cref="SagaPersistenceException" />.
    /// Completed saga state is monotonic; implementations should reject attempts to mark a previously
    /// completed saga incomplete.
    /// </remarks>
    Task CompleteAsync<TData>(
        TData data,
        string version,
        CancellationToken cancellationToken = default)
        where TData : class, ISagaData, new();
}
