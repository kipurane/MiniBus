using System.Data.Common;
using MiniBus.Core.Headers;
using MiniBus.Core.Persistence;

namespace MiniBus.Persistence.Sql;

internal sealed class SqlMiniBusPersistenceSession : IMiniBusPersistenceSession
{
    private readonly DbConnection _connection;
    private readonly SqlTableNames _tableNames;
    private readonly SqlOutboxOperationSerializer _operationSerializer;

    public SqlMiniBusPersistenceSession(
        DbConnection connection,
        SqlTableNames tableNames,
        SqlOutboxOperationSerializer operationSerializer)
    {
        _connection = connection;
        _tableNames = tableNames;
        _operationSerializer = operationSerializer;
    }

    public async Task<bool> IsProcessedAsync(
        MiniBusInboxMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        await using var command = _connection.CreateCommand();
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

        await using var transaction = await _connection
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        try
        {
            await InsertInboxRecordAsync(message, transaction, cancellationToken).ConfigureAwait(false);

            foreach (var operation in outboxOperations)
            {
                await InsertOutboxOperationAsync(message, operation, transaction, cancellationToken)
                    .ConfigureAwait(false);
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
        await _connection.DisposeAsync().ConfigureAwait(false);
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
        DbTransaction transaction,
        CancellationToken cancellationToken)
    {
        var serialized = _operationSerializer.Serialize(operation);

        await using var command = _connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
            INSERT INTO {_tableNames.Outbox}
                (Id, EndpointName, IncomingMessageId, OperationKind, MessageType, Body, HeadersJson, DueTime, CreatedUtc)
            VALUES
                (@Id, @EndpointName, @IncomingMessageId, @OperationKind, @MessageType, @Body, @HeadersJson, @DueTime, @CreatedUtc);
            """;
        AddParameter(command, "@Id", Guid.NewGuid());
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

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
