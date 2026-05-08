namespace MiniBus.Core.Sagas;

using System.Reflection;
using System.Text.Json;

public sealed class InMemorySagaPersistence : ISagaPersistence
{
    private static readonly JsonSerializerOptions CloneSerializerOptions = new(JsonSerializerDefaults.Web);
    private static readonly Dictionary<Type, PropertyInfo[]> ClonePropertiesByType = new();

    private readonly Dictionary<(Type DataType, string CorrelationId), StoredSaga> _sagas = new();

    public Task<SagaPersistenceRecord<TData>?> LoadAsync<TData>(
        string correlationId,
        CancellationToken cancellationToken = default)
        where TData : class, ISagaData, new()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        if (!_sagas.TryGetValue((typeof(TData), correlationId), out var storedSaga))
        {
            return Task.FromResult<SagaPersistenceRecord<TData>?>(null);
        }

        if (storedSaga.Data is not TData data)
        {
            throw new SagaPersistenceException($"Stored saga data for correlation id '{correlationId}' is not assignable to '{typeof(TData).FullName}'.");
        }

        return Task.FromResult<SagaPersistenceRecord<TData>?>(new SagaPersistenceRecord<TData>(Clone(data), storedSaga.Version.ToString()));
    }

    public Task CreateAsync<TData>(
        TData data,
        CancellationToken cancellationToken = default)
        where TData : class, ISagaData, new()
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentException.ThrowIfNullOrWhiteSpace(data.CorrelationId);

        var key = (typeof(TData), data.CorrelationId);
        if (_sagas.ContainsKey(key))
        {
            throw new SagaPersistenceException($"Saga data '{typeof(TData).FullName}' with correlation id '{data.CorrelationId}' already exists.");
        }

        _sagas.Add(key, new StoredSaga(Clone(data), Version: 1));
        return Task.CompletedTask;
    }

    public Task SaveAsync<TData>(
        TData data,
        string? version,
        CancellationToken cancellationToken = default)
        where TData : class, ISagaData, new()
    {
        return StoreExisting(data, version, complete: false);
    }

    public Task CompleteAsync<TData>(
        TData data,
        string? version,
        CancellationToken cancellationToken = default)
        where TData : class, ISagaData, new()
    {
        data.IsCompleted = true;
        return StoreExisting(data, version, complete: true);
    }

    private Task StoreExisting<TData>(TData data, string? version, bool complete)
        where TData : class, ISagaData, new()
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentException.ThrowIfNullOrWhiteSpace(data.CorrelationId);

        var key = (typeof(TData), data.CorrelationId);
        if (!_sagas.TryGetValue(key, out var storedSaga))
        {
            throw new SagaPersistenceException($"Saga data '{typeof(TData).FullName}' with correlation id '{data.CorrelationId}' does not exist.");
        }

        if (!string.IsNullOrWhiteSpace(version)
            && !string.Equals(version, storedSaga.Version.ToString(), StringComparison.Ordinal))
        {
            throw new SagaPersistenceException($"Saga data '{typeof(TData).FullName}' with correlation id '{data.CorrelationId}' has a stale version.");
        }

        data.IsCompleted = complete || data.IsCompleted;
        _sagas[key] = new StoredSaga(Clone(data), storedSaga.Version + 1);
        return Task.CompletedTask;
    }

    private static TData Clone<TData>(TData source)
        where TData : class, ISagaData, new()
    {
        var clone = new TData();
        foreach (var property in GetCloneProperties(typeof(TData)))
        {
            var value = property.GetValue(source);
            if (value is null)
            {
                property.SetValue(clone, null);
                continue;
            }

            var json = JsonSerializer.Serialize(value, property.PropertyType, CloneSerializerOptions);
            var clonedValue = JsonSerializer.Deserialize(json, property.PropertyType, CloneSerializerOptions);
            property.SetValue(clone, clonedValue);
        }

        return clone;
    }

    private static PropertyInfo[] GetCloneProperties(Type dataType)
    {
        if (ClonePropertiesByType.TryGetValue(dataType, out var properties))
        {
            return properties;
        }

        properties = dataType
            .GetProperties()
            .Where(property => property.CanRead && property.CanWrite)
            .ToArray();
        ClonePropertiesByType.Add(dataType, properties);
        return properties;
    }

    private sealed record StoredSaga(object Data, long Version);
}
