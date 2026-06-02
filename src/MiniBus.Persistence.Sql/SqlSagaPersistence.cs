using System.Data;
using System.Data.Common;
using MiniBus.Core.Sagas;

namespace MiniBus.Persistence.Sql;

public sealed class SqlSagaPersistence : ISagaPersistence
{
    private readonly MiniBusSqlPersistenceOptions _options;
    private readonly SqlSagaPersistenceOperations _operations;

    public SqlSagaPersistence(
        MiniBusSqlPersistenceOptions options,
        SqlSagaDataSerializer serializer)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(serializer);

        _options = options;
        _operations = new SqlSagaPersistenceOperations(serializer, new SqlTableNames(options));
    }

    public async Task<SagaPersistenceRecord<TData>?> LoadAsync<TData>(
        string correlationId,
        CancellationToken cancellationToken = default)
        where TData : class, ISagaData, new()
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await _operations.LoadAsync<TData>(
                connection,
                transaction: null,
                correlationId,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task CreateAsync<TData>(
        TData data,
        CancellationToken cancellationToken = default)
        where TData : class, ISagaData, new()
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await _operations.CreateAsync(
                connection,
                transaction: null,
                data,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public Task SaveAsync<TData>(
        TData data,
        string version,
        CancellationToken cancellationToken = default)
        where TData : class, ISagaData, new()
    {
        return StoreExistingAsync(data, version, complete: false, cancellationToken);
    }

    public Task CompleteAsync<TData>(
        TData data,
        string version,
        CancellationToken cancellationToken = default)
        where TData : class, ISagaData, new()
    {
        return StoreExistingAsync(data, version, complete: true, cancellationToken);
    }

    private async Task StoreExistingAsync<TData>(
        TData data,
        string version,
        bool complete,
        CancellationToken cancellationToken)
        where TData : class, ISagaData, new()
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        if (complete)
        {
            await _operations.CompleteAsync(
                    connection,
                    transaction: null,
                    data,
                    version,
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        await _operations.SaveAsync(
                connection,
                transaction: null,
                data,
                version,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        if (_options.ConnectionFactory is null)
        {
            throw new InvalidOperationException("MiniBus SQL persistence requires a SQL Server connection string or DbConnection factory.");
        }

        var connection = _options.ConnectionFactory();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        }

        return connection;
    }

}
