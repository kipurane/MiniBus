using System.Data.Common;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MiniBus.Core.Headers;
using MiniBus.Core.Persistence;

namespace MiniBus.Persistence.Sql;

internal sealed partial class SqlMiniBusPersistenceSession : IMiniBusPersistenceSession
{
    private readonly DbConnection _connection;
    private readonly bool _usesExternalTransaction;
    private readonly bool _ownsConnection;
    private readonly SqlTableNames _tableNames;
    private readonly SqlOutboxOperationSerializer _operationSerializer;
    private readonly ISqlMiniBusOutboxDispatchSignal _dispatchSignal;
    private readonly ILogger<SqlMiniBusPersistenceSession> _logger;
    private DbTransaction? _activeTransaction;
    private bool _inboxRecordInserted;

    public SqlMiniBusPersistenceSession(
        DbConnection connection,
        DbTransaction? transaction,
        bool ownsConnection,
        SqlTableNames tableNames,
        SqlOutboxOperationSerializer operationSerializer,
        ISqlMiniBusOutboxDispatchSignal dispatchSignal,
        ILogger<SqlMiniBusPersistenceSession>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(tableNames);
        ArgumentNullException.ThrowIfNull(operationSerializer);
        ArgumentNullException.ThrowIfNull(dispatchSignal);

        _connection = connection;
        _activeTransaction = transaction;
        _usesExternalTransaction = transaction is not null;
        _ownsConnection = ownsConnection;
        _tableNames = tableNames;
        _operationSerializer = operationSerializer;
        _dispatchSignal = dispatchSignal;
        _logger = logger ?? NullLogger<SqlMiniBusPersistenceSession>.Instance;
    }

    public async Task<bool> TryBeginAsync(
        MiniBusInboxMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (_inboxRecordInserted)
        {
            return true;
        }

        var transaction = await EnsureTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await InsertInboxRecordAsync(message, transaction, cancellationToken).ConfigureAwait(false);
            _inboxRecordInserted = true;
            return true;
        }
        catch (SqlException exception) when (IsDuplicateKey(exception))
        {
            if (!_usesExternalTransaction)
            {
                await RollbackOwnedTransactionAsync(cancellationToken).ConfigureAwait(false);
            }

            return false;
        }
    }

    public async Task<bool> IsProcessedAsync(
        MiniBusInboxMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        await using var command = _connection.CreateCommand();
        command.Transaction = _activeTransaction;
        command.CommandText = $"""
            SELECT 1
            FROM {_tableNames.Inbox}
            WHERE EndpointName = @EndpointName AND MessageId = @MessageId;
            """;
        AddParameter(command, "@EndpointName", message.EndpointName);
        AddParameter(command, "@MessageId", message.MessageId);

        return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is not null;
    }

    public async Task CommitAsync(
        MiniBusInboxMessage message,
        IReadOnlyCollection<MiniBusOutboxOperation> outboxOperations,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(outboxOperations);

        if (_usesExternalTransaction)
        {
            await CommitWithinExistingTransactionAsync(message, outboxOperations, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var transaction = await EnsureTransactionAsync(cancellationToken).ConfigureAwait(false);
        var committedMiniBusOwnedTransaction = false;

        try
        {
            if (!_inboxRecordInserted)
            {
                await InsertInboxRecordAsync(message, transaction, cancellationToken).ConfigureAwait(false);
                _inboxRecordInserted = true;
            }

            var sequence = 0;
            foreach (var operation in outboxOperations)
            {
                await InsertOutboxOperationAsync(message, operation, sequence, transaction, cancellationToken)
                    .ConfigureAwait(false);
                sequence++;
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            committedMiniBusOwnedTransaction = true;
        }
        catch
        {
            await RollbackOwnedTransactionAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }

        await DisposeOwnedTransactionAsync().ConfigureAwait(false);
        _inboxRecordInserted = false;

        if (committedMiniBusOwnedTransaction && outboxOperations.Count > 0)
        {
            WakeDispatcherBestEffort();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (!_usesExternalTransaction && _activeTransaction is not null)
        {
            await RollbackOwnedTransactionAsync().ConfigureAwait(false);
        }

        if (_ownsConnection)
        {
            await _connection.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task CommitWithinExistingTransactionAsync(
        MiniBusInboxMessage message,
        IReadOnlyCollection<MiniBusOutboxOperation> outboxOperations,
        CancellationToken cancellationToken)
    {
        if (!_inboxRecordInserted)
        {
            await InsertInboxRecordAsync(message, _activeTransaction!, cancellationToken).ConfigureAwait(false);
            _inboxRecordInserted = true;
        }

        var sequence = 0;
        foreach (var operation in outboxOperations)
        {
            await InsertOutboxOperationAsync(message, operation, sequence, _activeTransaction!, cancellationToken)
                .ConfigureAwait(false);
            sequence++;
        }
    }

    private async Task<DbTransaction> EnsureTransactionAsync(CancellationToken cancellationToken)
    {
        if (_activeTransaction is not null)
        {
            return _activeTransaction;
        }

        _activeTransaction = await _connection
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);
        return _activeTransaction;
    }

    private async Task RollbackOwnedTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_activeTransaction is null)
        {
            _inboxRecordInserted = false;
            return;
        }

        try
        {
            await _activeTransaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _inboxRecordInserted = false;
            await DisposeOwnedTransactionAsync().ConfigureAwait(false);
        }
    }

    private async Task DisposeOwnedTransactionAsync()
    {
        if (_activeTransaction is null)
        {
            return;
        }

        await _activeTransaction.DisposeAsync().ConfigureAwait(false);
        _activeTransaction = null;
    }

    private void WakeDispatcherBestEffort()
    {
        try
        {
            _dispatchSignal.Wake();
        }
        catch (Exception exception) when (IsBestEffortWakeFailure(exception))
        {
            LogWakeFailed(_logger, exception);
        }
    }

    private static bool IsBestEffortWakeFailure(Exception exception)
    {
        return exception is not OutOfMemoryException
            and not StackOverflowException
            and not AccessViolationException
            and not AppDomainUnloadedException;
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Debug,
        Message = "MiniBus SQL outbox dispatch wake-up failed after a successful commit.")]
    private static partial void LogWakeFailed(ILogger logger, Exception exception);

    private async Task InsertInboxRecordAsync(
        MiniBusInboxMessage message,
        DbTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = _connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
            INSERT INTO {_tableNames.Inbox}
                (EndpointName, MessageId, ProcessedUtc, HeadersJson, CorrelationId)
            VALUES
                (@EndpointName, @MessageId, @ProcessedUtc, @HeadersJson, @CorrelationId);
            """;
        AddParameter(command, "@EndpointName", message.EndpointName);
        AddParameter(command, "@MessageId", message.MessageId);
        AddParameter(command, "@ProcessedUtc", message.ProcessedUtc);
        AddParameter(command, "@HeadersJson", SqlOutboxOperationSerializer.SerializeHeaders(message.Headers));
        AddParameter(
            command,
            "@CorrelationId",
            message.Headers.TryGetValue(MiniBusHeaderNames.CorrelationId, out var correlationId)
                ? correlationId
                : DBNull.Value);

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task InsertOutboxOperationAsync(
        MiniBusInboxMessage inboxMessage,
        MiniBusOutboxOperation operation,
        int sequence,
        DbTransaction transaction,
        CancellationToken cancellationToken)
    {
        var serialized = await _operationSerializer
            .SerializeAsync(operation, cancellationToken)
            .ConfigureAwait(false);
        var outgoingMessageId = CreateOutgoingMessageId(inboxMessage, serialized, sequence);

        await using var command = _connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
            INSERT INTO {_tableNames.Outbox}
                (Id, OutgoingMessageId, EndpointName, IncomingMessageId, OperationKind, MessageType, Body, HeadersJson, CorrelationId, DueTime, CreatedUtc)
            VALUES
                (@Id, @OutgoingMessageId, @EndpointName, @IncomingMessageId, @OperationKind, @MessageType, @Body, @HeadersJson, @CorrelationId, @DueTime, @CreatedUtc);
            """;
        AddParameter(command, "@Id", Guid.NewGuid());
        AddParameter(command, "@OutgoingMessageId", outgoingMessageId);
        AddParameter(command, "@EndpointName", inboxMessage.EndpointName);
        AddParameter(command, "@IncomingMessageId", inboxMessage.MessageId);
        AddParameter(command, "@OperationKind", serialized.OperationKind);
        AddParameter(command, "@MessageType", serialized.MessageType);
        AddParameter(command, "@Body", serialized.Body);
        AddParameter(command, "@HeadersJson", serialized.HeadersJson);
        AddParameter(command, "@CorrelationId", ExtractCorrelationId(serialized.HeadersJson) is { } correlationId
            ? correlationId
            : DBNull.Value);
        AddParameter(command, "@DueTime", serialized.DueTime is null ? DBNull.Value : serialized.DueTime.Value);
        AddParameter(command, "@CreatedUtc", DateTimeOffset.UtcNow);

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string? ExtractCorrelationId(string headersJson)
    {
        return SqlOutboxOperationSerializer.DeserializeHeaders(headersJson)
            .TryGetValue(MiniBusHeaderNames.CorrelationId, out var correlationId)
            ? correlationId
            : null;
    }

    /// <summary>
    /// Creates the stable transport message id for one stored outbox operation.
    /// </summary>
    /// <remarks>
    /// The endpoint and incoming message id identify the handler execution being replayed, the operation sequence
    /// distinguishes multiple outgoing operations captured from that execution, and the operation kind plus message
    /// type make the identity resilient to mixed send/publish/schedule operations in the same batch. Hashing those
    /// stable inputs with SHA-256 produces a compact, deterministic id that can be reused after dispatcher crashes
    /// without exposing serialized message bodies or depending on random row ids.
    /// </remarks>
    private static string CreateOutgoingMessageId(
        MiniBusInboxMessage inboxMessage,
        SerializedOutboxOperation operation,
        int sequence)
    {
        var value = string.Join(
            "|",
            inboxMessage.EndpointName,
            inboxMessage.MessageId,
            sequence.ToString(CultureInfo.InvariantCulture),
            operation.OperationKind,
            operation.MessageType);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static bool IsDuplicateKey(SqlException exception)
    {
        return exception.Number is 2601 or 2627;
    }
}
