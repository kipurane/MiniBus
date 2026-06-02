using System.Data;
using System.Data.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MiniBus.Core.Persistence;
using MiniBus.Core.Serialization;

namespace MiniBus.Persistence.Sql;

public sealed class SqlMiniBusPersistenceSessionFactory : IMiniBusPersistenceSessionFactory
{
    private readonly MiniBusSqlPersistenceOptions _options;
    private readonly SqlOutboxOperationSerializer _operationSerializer;
    private readonly SqlSagaDataSerializer _sagaDataSerializer;
    private readonly SqlTableNames _tableNames;
    private readonly ISqlMiniBusOutboxDispatchSignal _dispatchSignal;
    private readonly ILogger<SqlMiniBusPersistenceSession> _sessionLogger;

    /// <summary>
    /// Creates a SQL persistence session factory using the same MiniBus message serializer
    /// for outbox operations and saga data.
    /// </summary>
    public SqlMiniBusPersistenceSessionFactory(
        MiniBusSqlPersistenceOptions options,
        IMessageSerializer messageSerializer)
        : this(
            options,
            CreateOutboxOperationSerializer(messageSerializer),
            CreateSagaDataSerializer(messageSerializer),
            new NoopSqlMiniBusOutboxDispatchSignal(),
            NullLogger<SqlMiniBusPersistenceSession>.Instance)
    {
    }

    /// <summary>
    /// Creates a SQL persistence session factory using the same MiniBus message serializer
    /// for outbox operations and saga data.
    /// </summary>
    public SqlMiniBusPersistenceSessionFactory(
        MiniBusSqlPersistenceOptions options,
        IMessageSerializer messageSerializer,
        ISqlMiniBusOutboxDispatchSignal dispatchSignal,
        ILoggerFactory? loggerFactory = null)
        : this(
            options,
            CreateOutboxOperationSerializer(messageSerializer),
            CreateSagaDataSerializer(messageSerializer),
            dispatchSignal,
            loggerFactory?.CreateLogger<SqlMiniBusPersistenceSession>())
    {
    }

    /// <summary>
    /// Creates a SQL persistence session factory with the supplied outbox operation serializer.
    /// Saga data uses <see cref="SystemTextJsonMessageSerializer"/> unless a saga serializer is
    /// supplied through a more specific constructor.
    /// </summary>
    [Obsolete("This overload can make saga serialization differ from outbox serialization. Use the IMessageSerializer overload to share one serializer, or the overload that accepts SqlSagaDataSerializer when the difference is intentional.")]
    public SqlMiniBusPersistenceSessionFactory(
        MiniBusSqlPersistenceOptions options,
        SqlOutboxOperationSerializer operationSerializer)
        : this(
            options,
            operationSerializer,
            new SqlSagaDataSerializer(new SystemTextJsonMessageSerializer()),
            new NoopSqlMiniBusOutboxDispatchSignal(),
            NullLogger<SqlMiniBusPersistenceSession>.Instance)
    {
    }

    /// <summary>
    /// Creates a SQL persistence session factory with the supplied outbox operation serializer.
    /// Saga data uses <see cref="SystemTextJsonMessageSerializer"/> unless a saga serializer is
    /// supplied through a more specific constructor.
    /// </summary>
    [Obsolete("This overload can make saga serialization differ from outbox serialization. Use the IMessageSerializer overload to share one serializer, or the overload that accepts SqlSagaDataSerializer when the difference is intentional.")]
    public SqlMiniBusPersistenceSessionFactory(
        MiniBusSqlPersistenceOptions options,
        SqlOutboxOperationSerializer operationSerializer,
        ISqlMiniBusOutboxDispatchSignal dispatchSignal,
        ILoggerFactory? loggerFactory = null)
        : this(
            options,
            operationSerializer,
            new SqlSagaDataSerializer(new SystemTextJsonMessageSerializer()),
            dispatchSignal,
            loggerFactory?.CreateLogger<SqlMiniBusPersistenceSession>())
    {
    }

    /// <summary>
    /// Creates a SQL persistence session factory with explicit outbox and saga serializers.
    /// Use this overload when saga data serialization intentionally differs from outbox message serialization.
    /// </summary>
    public SqlMiniBusPersistenceSessionFactory(
        MiniBusSqlPersistenceOptions options,
        SqlOutboxOperationSerializer operationSerializer,
        SqlSagaDataSerializer sagaDataSerializer,
        ISqlMiniBusOutboxDispatchSignal dispatchSignal,
        ILoggerFactory? loggerFactory = null)
        : this(
            options,
            operationSerializer,
            sagaDataSerializer,
            dispatchSignal,
            loggerFactory?.CreateLogger<SqlMiniBusPersistenceSession>())
    {
    }

    internal SqlMiniBusPersistenceSessionFactory(
        MiniBusSqlPersistenceOptions options,
        SqlOutboxOperationSerializer operationSerializer,
        SqlSagaDataSerializer sagaDataSerializer,
        ISqlMiniBusOutboxDispatchSignal dispatchSignal,
        ILogger<SqlMiniBusPersistenceSession>? sessionLogger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(operationSerializer);
        ArgumentNullException.ThrowIfNull(sagaDataSerializer);
        ArgumentNullException.ThrowIfNull(dispatchSignal);

        _options = options;
        _operationSerializer = operationSerializer;
        _sagaDataSerializer = sagaDataSerializer;
        _tableNames = new SqlTableNames(options);
        _dispatchSignal = dispatchSignal;
        _sessionLogger = sessionLogger ?? NullLogger<SqlMiniBusPersistenceSession>.Instance;
    }

    private static SqlOutboxOperationSerializer CreateOutboxOperationSerializer(IMessageSerializer messageSerializer)
    {
        ArgumentNullException.ThrowIfNull(messageSerializer);
        return new SqlOutboxOperationSerializer(messageSerializer);
    }

    private static SqlSagaDataSerializer CreateSagaDataSerializer(IMessageSerializer messageSerializer)
    {
        ArgumentNullException.ThrowIfNull(messageSerializer);
        return new SqlSagaDataSerializer(messageSerializer);
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
            _operationSerializer,
            _sagaDataSerializer,
            _dispatchSignal,
            _sessionLogger);
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
            _operationSerializer,
            _sagaDataSerializer,
            _dispatchSignal,
            _sessionLogger);
    }
}
