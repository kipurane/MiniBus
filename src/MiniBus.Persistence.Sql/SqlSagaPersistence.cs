using System.Data;
using System.Data.Common;
using Microsoft.Data.SqlClient;
using MiniBus.Core.Sagas;

namespace MiniBus.Persistence.Sql;

public sealed class SqlSagaPersistence : ISagaPersistence
{
    private readonly MiniBusSqlPersistenceOptions _options;
    private readonly SqlSagaDataSerializer _serializer;
    private readonly SqlTableNames _tableNames;

    public SqlSagaPersistence(
        MiniBusSqlPersistenceOptions options,
        SqlSagaDataSerializer serializer)
    {
        _options = options;
        _serializer = serializer;
        _tableNames = new SqlTableNames(options);
    }

    public async Task<SagaPersistenceRecord<TData>?> LoadAsync<TData>(
        string correlationId,
        CancellationToken cancellationToken = default)
        where TData : class, ISagaData, new()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT Data, Version
            FROM {_tableNames.Sagas}
            WHERE DataType = @DataType AND CorrelationId = @CorrelationId;
            """;
        AddParameter(command, "@DataType", SqlSagaDataSerializer.GetDataTypeName(typeof(TData)));
        AddParameter(command, "@CorrelationId", correlationId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var data = _serializer.Deserialize<TData>((byte[])reader["Data"]);
        var version = EncodeVersion((byte[])reader["Version"]);

        return new SagaPersistenceRecord<TData>(data, version);
    }

    public async Task CreateAsync<TData>(
        TData data,
        CancellationToken cancellationToken = default)
        where TData : class, ISagaData, new()
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentException.ThrowIfNullOrWhiteSpace(data.CorrelationId);

        var serialized = _serializer.Serialize(data);
        var now = DateTimeOffset.UtcNow;

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            INSERT INTO {_tableNames.Sagas}
                (Id, DataType, CorrelationId, Data, IsCompleted, CreatedUtc, UpdatedUtc, CompletedUtc)
            VALUES
                (@Id, @DataType, @CorrelationId, @Data, @IsCompleted, @CreatedUtc, @UpdatedUtc, @CompletedUtc);
            """;
        AddParameter(command, "@Id", data.Id);
        AddParameter(command, "@DataType", serialized.DataType);
        AddParameter(command, "@CorrelationId", data.CorrelationId);
        AddParameter(command, "@Data", serialized.Body);
        AddParameter(command, "@IsCompleted", data.IsCompleted);
        AddParameter(command, "@CreatedUtc", now);
        AddParameter(command, "@UpdatedUtc", now);
        AddParameter(command, "@CompletedUtc", data.IsCompleted ? now : DBNull.Value);

        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (SqlException exception) when (IsDuplicateKey(exception))
        {
            throw new SagaPersistenceException(
                $"Saga data '{typeof(TData).FullName}' with correlation id '{data.CorrelationId}' already exists.");
        }
        catch (SqlException exception)
        {
            throw new SagaPersistenceException(
                $"Failed to create saga data '{typeof(TData).FullName}' with correlation id '{data.CorrelationId}': {exception.Message}");
        }
    }

    public Task SaveAsync<TData>(
        TData data,
        string? version,
        CancellationToken cancellationToken = default)
        where TData : class, ISagaData, new()
    {
        return StoreExistingAsync(data, version, complete: false, cancellationToken);
    }

    public Task CompleteAsync<TData>(
        TData data,
        string? version,
        CancellationToken cancellationToken = default)
        where TData : class, ISagaData, new()
    {
        return StoreExistingAsync(data, version, complete: true, cancellationToken);
    }

    private async Task StoreExistingAsync<TData>(
        TData data,
        string? version,
        bool complete,
        CancellationToken cancellationToken)
        where TData : class, ISagaData, new()
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentException.ThrowIfNullOrWhiteSpace(data.CorrelationId);

        data.IsCompleted = complete || data.IsCompleted;
        var serialized = _serializer.Serialize(data);
        var expectedVersion = DecodeVersion(version);
        var now = DateTimeOffset.UtcNow;

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = expectedVersion is null
            ? $"""
            UPDATE {_tableNames.Sagas}
            SET Data = @Data,
                IsCompleted = @IsCompleted,
                UpdatedUtc = @UpdatedUtc,
                CompletedUtc = CASE
                    WHEN @IsCompleted = 1 AND CompletedUtc IS NULL THEN @CompletedUtc
                    WHEN @IsCompleted = 1 THEN CompletedUtc
                    ELSE NULL
                END
            WHERE DataType = @DataType
              AND CorrelationId = @CorrelationId;
            """
            : $"""
            UPDATE {_tableNames.Sagas}
            SET Data = @Data,
                IsCompleted = @IsCompleted,
                UpdatedUtc = @UpdatedUtc,
                CompletedUtc = CASE
                    WHEN @IsCompleted = 1 AND CompletedUtc IS NULL THEN @CompletedUtc
                    WHEN @IsCompleted = 1 THEN CompletedUtc
                    ELSE NULL
                END
            WHERE DataType = @DataType
              AND CorrelationId = @CorrelationId
              AND Version = @ExpectedVersion;
            """;
        AddParameter(command, "@Data", serialized.Body);
        AddParameter(command, "@IsCompleted", data.IsCompleted);
        AddParameter(command, "@UpdatedUtc", now);
        AddParameter(command, "@CompletedUtc", data.IsCompleted ? now : DBNull.Value);
        AddParameter(command, "@DataType", serialized.DataType);
        AddParameter(command, "@CorrelationId", data.CorrelationId);
        if (expectedVersion is not null)
        {
            AddParameter(command, "@ExpectedVersion", expectedVersion);
        }

        int affected;
        try
        {
            affected = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (SqlException exception)
        {
            throw new SagaPersistenceException(
                $"Failed to store saga data '{typeof(TData).FullName}' with correlation id '{data.CorrelationId}': {exception.Message}");
        }

        if (affected == 1)
        {
            return;
        }

        if (!await SagaExistsAsync<TData>(connection, data.CorrelationId, cancellationToken).ConfigureAwait(false))
        {
            throw new SagaPersistenceException(
                $"Saga data '{typeof(TData).FullName}' with correlation id '{data.CorrelationId}' does not exist.");
        }

        throw new SagaPersistenceException(
            $"Saga data '{typeof(TData).FullName}' with correlation id '{data.CorrelationId}' has a stale version.");
    }

    private async Task<bool> SagaExistsAsync<TData>(
        DbConnection connection,
        string correlationId,
        CancellationToken cancellationToken)
        where TData : class, ISagaData, new()
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT 1
            FROM {_tableNames.Sagas}
            WHERE DataType = @DataType AND CorrelationId = @CorrelationId;
            """;
        AddParameter(command, "@DataType", SqlSagaDataSerializer.GetDataTypeName(typeof(TData)));
        AddParameter(command, "@CorrelationId", correlationId);

        return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is not null;
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

    private static string EncodeVersion(byte[] version)
    {
        return Convert.ToBase64String(version);
    }

    private static byte[]? DecodeVersion(string? version)
    {
        return string.IsNullOrWhiteSpace(version)
            ? null
            : Convert.FromBase64String(version);
    }

    private static bool IsDuplicateKey(SqlException exception)
    {
        return exception.Number is 2601 or 2627;
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
