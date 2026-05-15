using System.Data.Common;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using MiniBus.Core.Headers;
using MiniBus.Core.Persistence;

namespace MiniBus.Persistence.Sql;

internal sealed class SqlMiniBusPersistenceSession : IMiniBusPersistenceSession
{
    private readonly DbConnection _connection;
    private readonly DbTransaction? _transaction;
    private readonly bool _ownsConnection;
    private readonly SqlTableNames _tableNames;
    private readonly SqlOutboxOperationSerializer _operationSerializer;

    public SqlMiniBusPersistenceSession(
        DbConnection connection,
        DbTransaction? transaction,
        bool ownsConnection,
        SqlTableNames tableNames,
        SqlOutboxOperationSerializer operationSerializer)
    {
        _connection = connection;
        _transaction = transaction;
        _ownsConnection = ownsConnection;
        _tableNames = tableNames;
        _operationSerializer = operationSerializer;
    }

    public async Task<bool> IsProcessedAsync(
        MiniBusInboxMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        await using var command = _connection.CreateCommand();
        command.Transaction = _transaction;
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

        if (_transaction is not null)
        {
            await CommitWithinExistingTransactionAsync(message, outboxOperations, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        await using var transaction = await _connection
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        try
        {
            await InsertInboxRecordAsync(message, transaction, cancellationToken).ConfigureAwait(false);

            var sequence = 0;
            foreach (var operation in outboxOperations)
            {
                await InsertOutboxOperationAsync(message, operation, sequence, transaction, cancellationToken)
                    .ConfigureAwait(false);
                sequence++;
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
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
        await InsertInboxRecordAsync(message, _transaction!, cancellationToken).ConfigureAwait(false);

        var sequence = 0;
        foreach (var operation in outboxOperations)
        {
            await InsertOutboxOperationAsync(message, operation, sequence, _transaction!, cancellationToken)
                .ConfigureAwait(false);
            sequence++;
        }
    }

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
                (Id, OutgoingMessageId, EndpointName, IncomingMessageId, OperationKind, MessageType, Body, HeadersJson, DueTime, CreatedUtc)
            VALUES
                (@Id, @OutgoingMessageId, @EndpointName, @IncomingMessageId, @OperationKind, @MessageType, @Body, @HeadersJson, @DueTime, @CreatedUtc);
            """;
        AddParameter(command, "@Id", Guid.NewGuid());
        AddParameter(command, "@OutgoingMessageId", outgoingMessageId);
        AddParameter(command, "@EndpointName", inboxMessage.EndpointName);
        AddParameter(command, "@IncomingMessageId", inboxMessage.MessageId);
        AddParameter(command, "@OperationKind", serialized.OperationKind);
        AddParameter(command, "@MessageType", serialized.MessageType);
        AddParameter(command, "@Body", serialized.Body);
        AddParameter(command, "@HeadersJson", serialized.HeadersJson);
        AddParameter(command, "@DueTime", serialized.DueTime is null ? DBNull.Value : serialized.DueTime.Value);
        AddParameter(command, "@CreatedUtc", DateTimeOffset.UtcNow);

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
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
}
