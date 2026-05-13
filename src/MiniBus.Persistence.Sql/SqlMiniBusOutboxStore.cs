using System.Data.Common;
using MiniBus.Core.Persistence;

namespace MiniBus.Persistence.Sql;

public sealed class SqlMiniBusOutboxStore : ISqlMiniBusOutboxStore
{
    private readonly MiniBusSqlPersistenceOptions _options;
    private readonly SqlOutboxOperationSerializer _operationSerializer;
    private readonly SqlTableNames _tableNames;
    private readonly int _claimLeaseSeconds;

    public SqlMiniBusOutboxStore(
        MiniBusSqlPersistenceOptions options,
        SqlOutboxOperationSerializer operationSerializer)
    {
        _options = options;
        _operationSerializer = operationSerializer;
        _tableNames = new SqlTableNames(options);
        _claimLeaseSeconds = GetClaimLeaseSeconds(options);
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
                inserted.OutgoingMessageId,
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
                  AND (ClaimedUtc IS NULL OR ClaimedUtc < DATEADD(second, -@ClaimLeaseSeconds, SYSUTCDATETIME()))
                ORDER BY CreatedUtc, Id
            ) AS pending;
            """;
        AddParameter(command, "@BatchSize", batchSize);
        AddParameter(command, "@ClaimLeaseSeconds", _claimLeaseSeconds);

        var operations = new List<MiniBusOutboxStoredOperation>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            operations.Add(_operationSerializer.Deserialize(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                (byte[])reader.GetValue(4),
                reader.GetString(5),
                reader.IsDBNull(6) ? null : (DateTimeOffset)reader.GetValue(6),
                reader.GetInt32(7)));
        }

        return operations;
    }

    public async Task<int> CleanupAsync(CancellationToken cancellationToken = default)
    {
        var deleted = 0;

        if (_options.InboxRetention is not null)
        {
            deleted += await ExecuteCleanupAsync(
                    $"""
                    DELETE TOP (@BatchSize)
                    FROM {_tableNames.Inbox}
                    WHERE ProcessedUtc < @CutoffUtc;
                    """,
                    DateTimeOffset.UtcNow.Subtract(_options.InboxRetention.Value),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (_options.DispatchedOutboxRetention is not null)
        {
            deleted += await ExecuteCleanupAsync(
                    $"""
                    DELETE TOP (@BatchSize)
                    FROM {_tableNames.Outbox}
                    WHERE DispatchedUtc IS NOT NULL
                      AND DispatchedUtc < @CutoffUtc;
                    """,
                    DateTimeOffset.UtcNow.Subtract(_options.DispatchedOutboxRetention.Value),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (_options.FailedOutboxRetention is not null)
        {
            deleted += await ExecuteCleanupAsync(
                    $"""
                    DELETE TOP (@BatchSize)
                    FROM {_tableNames.Outbox}
                    WHERE DispatchedUtc IS NULL
                      AND ClaimedUtc IS NULL
                      AND LastError IS NOT NULL
                      AND CreatedUtc < @CutoffUtc;
                    """,
                    DateTimeOffset.UtcNow.Subtract(_options.FailedOutboxRetention.Value),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return deleted;
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

    private async Task<int> ExecuteCleanupAsync(
        string commandText,
        DateTimeOffset cutoffUtc,
        CancellationToken cancellationToken)
    {
        if (_options.ConnectionFactory is null)
        {
            throw new InvalidOperationException("MiniBus SQL cleanup requires a SQL Server connection string or DbConnection factory.");
        }

        await using var connection = _options.ConnectionFactory();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        AddParameter(command, "@BatchSize", _options.CleanupBatchSize);
        AddParameter(command, "@CutoffUtc", cutoffUtc);

        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static int GetClaimLeaseSeconds(MiniBusSqlPersistenceOptions options)
    {
        if (options.OutboxClaimLeaseDuration <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("MiniBus SQL outbox claim lease duration must be greater than zero.");
        }

        return (int)Math.Ceiling(options.OutboxClaimLeaseDuration.TotalSeconds);
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
