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

        return new SqlMiniBusPersistenceSession(connection, _tableNames, _operationSerializer);
    }
}
