using System.Collections;
using System.Data;
using System.Data.Common;
using MiniBus.Core.Contracts;
using MiniBus.Core.Headers;
using MiniBus.Core.Persistence;
using MiniBus.Core.Serialization;
using Xunit;

namespace MiniBus.Persistence.Sql.Tests;

public sealed class SqlMiniBusPersistenceSessionWakeTests
{
    [Theory]
    [InlineData("connection")]
    [InlineData("tableNames")]
    [InlineData("operationSerializer")]
    [InlineData("dispatchSignal")]
    public void Constructor_ValidatesRequiredDependencies(string nullDependency)
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new SqlMiniBusPersistenceSession(
                nullDependency == "connection" ? null! : new RecordingDbConnection(),
                transaction: null,
                ownsConnection: true,
                nullDependency == "tableNames" ? null! : new SqlTableNames(new MiniBusSqlPersistenceOptions()),
                nullDependency == "operationSerializer" ? null! : new SqlOutboxOperationSerializer(new RecordingSerializer()),
                nullDependency == "dispatchSignal" ? null! : new RecordingDispatchSignal()));

        Assert.Equal(nullDependency, exception.ParamName);
    }

    [Fact]
    public async Task CommitAsync_WakesHostedDispatcherAfterSuccessfulMiniBusOwnedCommitWithOutboxOperations()
    {
        var connection = new RecordingDbConnection();
        var signal = new RecordingDispatchSignal();
        await using var session = CreateSession(
            connection,
            transaction: null,
            ownsConnection: true,
            signal);

        await session.CommitAsync(CreateInboxMessage(), new[] { CreateOutboxOperation() });

        Assert.Equal(1, signal.WakeCount);
        Assert.Equal(1, connection.LastTransaction?.CommitCount);
        Assert.Equal(0, connection.LastTransaction?.RollbackCount);
    }

    [Fact]
    public async Task CommitAsync_DoesNotWakeHostedDispatcherWhenMiniBusOwnedCommitFails()
    {
        var connection = new RecordingDbConnection
        {
            CommitException = new InvalidOperationException("commit failed")
        };
        var signal = new RecordingDispatchSignal();
        await using var session = CreateSession(
            connection,
            transaction: null,
            ownsConnection: true,
            signal);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            session.CommitAsync(CreateInboxMessage(), new[] { CreateOutboxOperation() }));

        Assert.Equal(0, signal.WakeCount);
        Assert.Equal(1, connection.LastTransaction?.CommitCount);
        Assert.Equal(1, connection.LastTransaction?.RollbackCount);
    }

    [Fact]
    public async Task CommitAsync_PreservesCommitSuccessWhenWakeThrows()
    {
        var connection = new RecordingDbConnection();
        var signal = new RecordingDispatchSignal
        {
            WakeException = new InvalidOperationException("wake failed")
        };
        await using var session = CreateSession(
            connection,
            transaction: null,
            ownsConnection: true,
            signal);

        await session.CommitAsync(CreateInboxMessage(), new[] { CreateOutboxOperation() });

        Assert.Equal(1, signal.WakeCount);
        Assert.Equal(1, connection.LastTransaction?.CommitCount);
        Assert.Equal(0, connection.LastTransaction?.RollbackCount);
    }

    [Fact]
    public async Task CommitAsync_DoesNotSuppressCriticalWakeExceptions()
    {
        var connection = new RecordingDbConnection();
        var signal = new RecordingDispatchSignal
        {
            WakeException = new OutOfMemoryException("critical wake failure")
        };
        await using var session = CreateSession(
            connection,
            transaction: null,
            ownsConnection: true,
            signal);

        await Assert.ThrowsAsync<OutOfMemoryException>(() =>
            session.CommitAsync(CreateInboxMessage(), new[] { CreateOutboxOperation() }));

        Assert.Equal(1, signal.WakeCount);
        Assert.Equal(1, connection.LastTransaction?.CommitCount);
        Assert.Equal(0, connection.LastTransaction?.RollbackCount);
    }

    [Fact]
    public async Task CommitAsync_DoesNotWakeHostedDispatcherForApplicationOwnedTransaction()
    {
        var connection = new RecordingDbConnection();
        var transaction = connection.CreateApplicationTransaction();
        var signal = new RecordingDispatchSignal
        {
            WakeException = new InvalidOperationException("wake should not run")
        };
        await using var session = CreateSession(
            connection,
            transaction,
            ownsConnection: false,
            signal);

        await session.CommitAsync(CreateInboxMessage(), new[] { CreateOutboxOperation() });

        Assert.Equal(0, signal.WakeCount);
        Assert.Equal(0, transaction.CommitCount);
        Assert.Equal(0, transaction.RollbackCount);
    }

    [Fact]
    public async Task PublicFactoryConstructor_UsesProvidedDispatchSignal()
    {
        var connection = new RecordingDbConnection();
        var signal = new RecordingDispatchSignal();
        var factory = new SqlMiniBusPersistenceSessionFactory(
            new MiniBusSqlPersistenceOptions
            {
                ConnectionFactory = () => connection
            },
            new SqlOutboxOperationSerializer(new RecordingSerializer()),
            signal);

        await using var session = await factory.CreateAsync();

        await session.CommitAsync(CreateInboxMessage(), new[] { CreateOutboxOperation() });

        Assert.Equal(1, signal.WakeCount);
        Assert.Equal(1, connection.LastTransaction?.CommitCount);
    }

    private static SqlMiniBusPersistenceSession CreateSession(
        DbConnection connection,
        DbTransaction? transaction,
        bool ownsConnection,
        ISqlMiniBusOutboxDispatchSignal signal)
    {
        return new SqlMiniBusPersistenceSession(
            connection,
            transaction,
            ownsConnection,
            new SqlTableNames(new MiniBusSqlPersistenceOptions()),
            new SqlOutboxOperationSerializer(new RecordingSerializer()),
            signal);
    }

    private static MiniBusInboxMessage CreateInboxMessage()
    {
        return new MiniBusInboxMessage(
            "Billing",
            "message-1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [MiniBusHeaderNames.CorrelationId] = "correlation-1"
            },
            DateTimeOffset.UtcNow);
    }

    private static MiniBusOutboxOperation CreateOutboxOperation()
    {
        return new MiniBusOutboxOperation(
            MiniBusOutboxOperationKind.Send,
            new TestCommand(Guid.NewGuid()),
            typeof(TestCommand),
            new Dictionary<string, string>(StringComparer.Ordinal),
            DueTime: null);
    }

    private sealed record TestCommand(Guid Id) : ICommand;

    private sealed class RecordingDispatchSignal : ISqlMiniBusOutboxDispatchSignal
    {
        public Exception? WakeException { get; init; }

        public int WakeCount { get; private set; }

        public void Wake()
        {
            WakeCount++;

            if (WakeException is not null)
            {
                throw WakeException;
            }
        }

        public ValueTask<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(false);
        }
    }

    private sealed class RecordingSerializer : IMessageSerializer
    {
        public BinaryData Serialize(object message, Type messageType)
        {
            return BinaryData.FromString($"serialized:{messageType.Name}");
        }

        public object Deserialize(BinaryData body, Type messageType)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class RecordingDbConnection : DbConnection
    {
        public Exception? CommitException { get; init; }

        public RecordingDbTransaction? LastTransaction { get; private set; }

        public int ExecuteNonQueryCount { get; private set; }

#pragma warning disable CS8764, CS8765
        public override string? ConnectionString { get; set; } = "Server=recording";
#pragma warning restore CS8764, CS8765

        public override string Database => "MiniBusTests";

        public override string DataSource => "Recording";

        public override string ServerVersion => "1.0";

        public override ConnectionState State { get; } = ConnectionState.Open;

        public RecordingDbTransaction CreateApplicationTransaction()
        {
            return new RecordingDbTransaction(this);
        }

        public override void ChangeDatabase(string databaseName)
        {
        }

        public override void Close()
        {
        }

        public override void Open()
        {
        }

        public override Task OpenAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        internal int RecordExecuteNonQuery()
        {
            ExecuteNonQueryCount++;
            return 1;
        }

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
        {
            LastTransaction = new RecordingDbTransaction(this)
            {
                CommitException = CommitException
            };
            return LastTransaction;
        }

        protected override DbCommand CreateDbCommand()
        {
            return new RecordingDbCommand(this);
        }
    }

    private sealed class RecordingDbTransaction : DbTransaction
    {
        private readonly RecordingDbConnection _connection;

        public RecordingDbTransaction(RecordingDbConnection connection)
        {
            _connection = connection;
        }

        public Exception? CommitException { get; init; }

        public int CommitCount { get; private set; }

        public int RollbackCount { get; private set; }

        public override IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;

        protected override DbConnection DbConnection => _connection;

        public override void Commit()
        {
            CommitCount++;

            if (CommitException is not null)
            {
                throw CommitException;
            }
        }

        public override Task CommitAsync(CancellationToken cancellationToken = default)
        {
            Commit();
            return Task.CompletedTask;
        }

        public override void Rollback()
        {
            RollbackCount++;
        }

        public override Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            Rollback();
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingDbCommand : DbCommand
    {
        private readonly RecordingDbConnection _connection;
        private readonly RecordingDbParameterCollection _parameters = new();

        public RecordingDbCommand(RecordingDbConnection connection)
        {
            _connection = connection;
        }

#pragma warning disable CS8764, CS8765
        public override string? CommandText { get; set; } = string.Empty;
#pragma warning restore CS8764, CS8765

        public override int CommandTimeout { get; set; }

        public override CommandType CommandType { get; set; }

        public override bool DesignTimeVisible { get; set; }

        public override UpdateRowSource UpdatedRowSource { get; set; }

#pragma warning disable CS8764, CS8765
        protected override DbConnection? DbConnection
        {
            get => _connection;
            set { }
        }
#pragma warning restore CS8764, CS8765

        protected override DbParameterCollection DbParameterCollection => _parameters;

        protected override DbTransaction? DbTransaction { get; set; }

        public override void Cancel()
        {
        }

        public override int ExecuteNonQuery()
        {
            return _connection.RecordExecuteNonQuery();
        }

        public override object? ExecuteScalar()
        {
            return null;
        }

        public override void Prepare()
        {
        }

        protected override DbParameter CreateDbParameter()
        {
            return new RecordingDbParameter();
        }

        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class RecordingDbParameter : DbParameter
    {
        public override DbType DbType { get; set; }

        public override ParameterDirection Direction { get; set; }

        public override bool IsNullable { get; set; }

#pragma warning disable CS8764, CS8765
        public override string? ParameterName { get; set; } = string.Empty;

        public override string? SourceColumn { get; set; } = string.Empty;
#pragma warning restore CS8764, CS8765

        public override object? Value { get; set; }

        public override bool SourceColumnNullMapping { get; set; }

        public override int Size { get; set; }

        public override void ResetDbType()
        {
        }
    }

    private sealed class RecordingDbParameterCollection : DbParameterCollection
    {
        private readonly List<DbParameter> _parameters = new();

        public override int Count => _parameters.Count;

        public override object SyncRoot => this;

        public override int Add(object value)
        {
            _parameters.Add((DbParameter)value);
            return _parameters.Count - 1;
        }

        public override void AddRange(Array values)
        {
            foreach (var value in values)
            {
                Add(value);
            }
        }

        public override void Clear()
        {
            _parameters.Clear();
        }

        public override bool Contains(object value)
        {
            return _parameters.Contains((DbParameter)value);
        }

        public override bool Contains(string value)
        {
            return _parameters.Any(parameter => parameter.ParameterName == value);
        }

        public override void CopyTo(Array array, int index)
        {
            ((ICollection)_parameters).CopyTo(array, index);
        }

        public override IEnumerator GetEnumerator()
        {
            return _parameters.GetEnumerator();
        }

        public override int IndexOf(object value)
        {
            return _parameters.IndexOf((DbParameter)value);
        }

        public override int IndexOf(string parameterName)
        {
            return _parameters.FindIndex(parameter => parameter.ParameterName == parameterName);
        }

        public override void Insert(int index, object value)
        {
            _parameters.Insert(index, (DbParameter)value);
        }

        public override void Remove(object value)
        {
            _parameters.Remove((DbParameter)value);
        }

        public override void RemoveAt(int index)
        {
            _parameters.RemoveAt(index);
        }

        public override void RemoveAt(string parameterName)
        {
            var index = IndexOf(parameterName);
            if (index >= 0)
            {
                RemoveAt(index);
            }
        }

        protected override DbParameter GetParameter(int index)
        {
            return _parameters[index];
        }

        protected override DbParameter GetParameter(string parameterName)
        {
            var index = IndexOf(parameterName);
            if (index < 0)
            {
                throw new IndexOutOfRangeException(parameterName);
            }

            return _parameters[index];
        }

        protected override void SetParameter(int index, DbParameter value)
        {
            _parameters[index] = value;
        }

        protected override void SetParameter(string parameterName, DbParameter value)
        {
            var index = IndexOf(parameterName);
            if (index < 0)
            {
                _parameters.Add(value);
                return;
            }

            _parameters[index] = value;
        }
    }
}
