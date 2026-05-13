using System.Data;
using System.Data.Common;
using MiniBus.Core.Persistence;

namespace MiniBus.Persistence.Sql;

public sealed class SqlMiniBusPersistenceSessionFactory : IMiniBusPersistenceSessionFactory
{
    private readonly MiniBusSqlPersistenceOptions _options;
    private readonly SqlOutboxOperationSerializer _operationSerializer;
    private readonly SqlTableNames _tableNames;

    public SqlMiniBusPersistenceSessionFactory(
        MiniBusSqlPersistenceOptions options,
        SqlOutboxOperationSerializer operationSerializer)
    {
        _options = options;
        _operationSerializer = operationSerializer;
        _tableNames = new SqlTableNames(options);
    }

    public async ValueTask<IMiniBusPersistenceSession> CreateAsync(
        CancellationToken cancellationToken = default)
    {
        if (_options.ConnectionFactory is null)
        {
            throw new InvalidOperationException("MiniBus SQL persistence requires a SQL Server connection string or DbConnection factory.");
        }

        var connection = _options.ConnectionFactory();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        return new SqlMiniBusPersistenceSession(
            connection,
            transaction: null,
            ownsConnection: true,
            _tableNames,
            _operationSerializer);
    }

    public IMiniBusPersistenceSession CreateForTransaction(
        DbConnection connection,
        DbTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);

        if (connection.State != ConnectionState.Open)
        {
            throw new InvalidOperationException("Application-owned MiniBus SQL transactions require an open DbConnection.");
        }

        if (transaction.Connection is null)
        {
            throw new InvalidOperationException("Application-owned MiniBus SQL transactions require an active DbTransaction.");
        }

        if (!ReferenceEquals(transaction.Connection, connection))
        {
            throw new InvalidOperationException("Application-owned MiniBus SQL transactions require the transaction to belong to the provided DbConnection.");
        }

        return new SqlMiniBusPersistenceSession(
            connection,
            transaction,
            ownsConnection: false,
            _tableNames,
            _operationSerializer);
    }
}
