using System.Data.Common;
using MiniBus.Core.Persistence;

namespace MiniBus.Persistence.Sql;

public sealed class SqlMiniBusOutboxStore : ISqlMiniBusOutboxStore
{
    private readonly MiniBusSqlPersistenceOptions _options;
    private readonly SqlOutboxOperationSerializer _operationSerializer;
    private readonly SqlTableNames _tableNames;

    public SqlMiniBusOutboxStore(
        MiniBusSqlPersistenceOptions options,
        SqlOutboxOperationSerializer operationSerializer)
    {
        _options = options;
        _operationSerializer = operationSerializer;
        _tableNames = new SqlTableNames(options);
    }

    public async Task<IReadOnlyList<MiniBusOutboxStoredOperation>> ClaimPendingAsync(
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        if (_options.ConnectionFactory is null)
        {
            throw new InvalidOperationException("MiniBus SQL outbox dispatch requires a SQL Server connection string or DbConnection factory.");
        }

        await using var connection = _options.ConnectionFactory();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            UPDATE pending
                SET ClaimedUtc = SYSUTCDATETIME(),
                    AttemptCount = AttemptCount + 1,
                    LastError = NULL
            OUTPUT
                inserted.Id,
                inserted.OperationKind,
                inserted.MessageType,
                inserted.Body,
                inserted.HeadersJson,
                inserted.DueTime,
                inserted.AttemptCount
            FROM (
                SELECT TOP (@BatchSize) *
                FROM {_tableNames.Outbox} WITH (UPDLOCK, READPAST, ROWLOCK)
                WHERE DispatchedUtc IS NULL
                  AND (ClaimedUtc IS NULL OR ClaimedUtc < DATEADD(minute, -5, SYSUTCDATETIME()))
                ORDER BY CreatedUtc, Id
            ) AS pending;
            """;
        AddParameter(command, "@BatchSize", batchSize);

        var operations = new List<MiniBusOutboxStoredOperation>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            operations.Add(_operationSerializer.Deserialize(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                (byte[])reader.GetValue(3),
                reader.GetString(4),
                reader.IsDBNull(5) ? null : (DateTimeOffset)reader.GetValue(5),
                reader.GetInt32(6)));
        }

        return operations;
    }

    public async Task MarkDispatchedAsync(
        Guid operationId,
        CancellationToken cancellationToken = default)
    {
        await ExecuteStatusUpdateAsync(
                operationId,
                "DispatchedUtc = SYSUTCDATETIME(), ClaimedUtc = NULL, LastError = NULL",
                error: null,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task MarkFailedAsync(
        Guid operationId,
        Exception exception,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(exception);

        await ExecuteStatusUpdateAsync(
                operationId,
                "ClaimedUtc = NULL, LastError = @LastError",
                exception.ToString(),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task ExecuteStatusUpdateAsync(
        Guid operationId,
        string setClause,
        string? error,
        CancellationToken cancellationToken)
    {
        if (_options.ConnectionFactory is null)
        {
            throw new InvalidOperationException("MiniBus SQL outbox dispatch requires a SQL Server connection string or DbConnection factory.");
        }

        await using var connection = _options.ConnectionFactory();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            UPDATE {_tableNames.Outbox}
            SET {setClause}
            WHERE Id = @Id;
            """;
        AddParameter(command, "@Id", operationId);

        if (error is not null)
        {
            AddParameter(command, "@LastError", error);
        }

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
