using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using System.Reflection;
using Microsoft.Data.SqlClient;
using MiniBus.Core.Sagas;

namespace MiniBus.Persistence.Sql;

internal sealed class SqlSagaPersistenceOperations
{
    private const int MaxSize = -1;
    private const int StoreResultSuccess = 1;
    private const int StoreResultStaleVersion = 0;
    private const int StoreResultMissingSaga = -1;
    private const int StoreResultCompletionRegression = -2;
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> ClonePropertiesByType = new();

    private readonly SqlSagaDataSerializer _serializer;
    private readonly SqlTableNames _tableNames;

    public SqlSagaPersistenceOperations(
        SqlSagaDataSerializer serializer,
        SqlTableNames tableNames)
    {
        ArgumentNullException.ThrowIfNull(serializer);
        ArgumentNullException.ThrowIfNull(tableNames);

        _serializer = serializer;
        _tableNames = tableNames;
    }

    public async Task<SagaPersistenceRecord<TData>?> LoadAsync<TData>(
        DbConnection connection,
        DbTransaction? transaction,
        string correlationId,
        CancellationToken cancellationToken)
        where TData : class, ISagaData, new()
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId, nameof(correlationId));

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
            SELECT Data, IsCompleted, Version
            FROM {_tableNames.Sagas}
            WHERE DataType = @DataType AND CorrelationId = @CorrelationId;
            """;
        AddStringParameter(command, "@DataType", SqlSagaDataSerializer.GetDataTypeName(typeof(TData)), SqlSagaSchema.DataTypeMaxLength);
        AddStringParameter(command, "@CorrelationId", correlationId, SqlSagaSchema.CorrelationIdMaxLength);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var data = _serializer.Deserialize<TData>((byte[])reader["Data"]);
        data.IsCompleted = (bool)reader["IsCompleted"];
        var version = EncodeVersion((byte[])reader["Version"]);

        return new SagaPersistenceRecord<TData>(data, version);
    }

    public async Task CreateAsync<TData>(
        DbConnection connection,
        DbTransaction? transaction,
        TData data,
        CancellationToken cancellationToken)
        where TData : class, ISagaData, new()
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(data);
        ArgumentException.ThrowIfNullOrWhiteSpace(data.CorrelationId, nameof(ISagaData.CorrelationId));

        var serialized = _serializer.Serialize(data);
        var now = DateTimeOffset.UtcNow;

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
            INSERT INTO {_tableNames.Sagas}
                (Id, DataType, CorrelationId, Data, IsCompleted, CreatedUtc, UpdatedUtc, CompletedUtc)
            VALUES
                (@Id, @DataType, @CorrelationId, @Data, @IsCompleted, @CreatedUtc, @UpdatedUtc, @CompletedUtc);
            """;
        AddGuidParameter(command, "@Id", data.Id);
        AddStringParameter(command, "@DataType", serialized.DataType, SqlSagaSchema.DataTypeMaxLength);
        AddStringParameter(command, "@CorrelationId", data.CorrelationId, SqlSagaSchema.CorrelationIdMaxLength);
        AddBytesParameter(command, "@Data", serialized.Body, MaxSize);
        AddBooleanParameter(command, "@IsCompleted", data.IsCompleted);
        AddDateTimeOffsetParameter(command, "@CreatedUtc", now);
        AddDateTimeOffsetParameter(command, "@UpdatedUtc", now);
        AddDateTimeOffsetParameter(command, "@CompletedUtc", data.IsCompleted ? now : null);

        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (SqlException exception) when (IsDuplicateKey(exception))
        {
            throw new SagaPersistenceException(
                $"Saga data '{typeof(TData).FullName}' with correlation id '{data.CorrelationId}' already exists.",
                exception);
        }
        catch (SqlException exception)
        {
            throw new SagaPersistenceException(
                $"Failed to create saga data '{typeof(TData).FullName}' with correlation id '{data.CorrelationId}': {exception.Message}",
                exception);
        }
    }

    public Task SaveAsync<TData>(
        DbConnection connection,
        DbTransaction? transaction,
        TData data,
        string version,
        CancellationToken cancellationToken)
        where TData : class, ISagaData, new()
    {
        return StoreExistingAsync(connection, transaction, data, version, complete: false, cancellationToken);
    }

    public Task CompleteAsync<TData>(
        DbConnection connection,
        DbTransaction? transaction,
        TData data,
        string version,
        CancellationToken cancellationToken)
        where TData : class, ISagaData, new()
    {
        return StoreExistingAsync(connection, transaction, data, version, complete: true, cancellationToken);
    }

    private async Task StoreExistingAsync<TData>(
        DbConnection connection,
        DbTransaction? transaction,
        TData data,
        string version,
        bool complete,
        CancellationToken cancellationToken)
        where TData : class, ISagaData, new()
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(data);
        ArgumentException.ThrowIfNullOrWhiteSpace(data.CorrelationId, nameof(ISagaData.CorrelationId));
        ArgumentException.ThrowIfNullOrWhiteSpace(version, nameof(version));

        var isCompleted = complete || data.IsCompleted;
        var serialized = _serializer.Serialize(CreateSerializedData(data, isCompleted));
        var expectedVersion = DecodeVersion(version);
        var now = DateTimeOffset.UtcNow;

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
            SET NOCOUNT ON;

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
                            AND Version = @ExpectedVersion
                            AND (@IsCompleted = 1 OR IsCompleted = 0);

            IF @@ROWCOUNT = 1
                                SET @StoreResult = {StoreResultSuccess};
                        ELSE IF EXISTS (
                                SELECT 1
                                FROM {_tableNames.Sagas}
                                WHERE DataType = @DataType
                                    AND CorrelationId = @CorrelationId
                                    AND Version = @ExpectedVersion
                                    AND IsCompleted = 1)
                                SET @StoreResult = {StoreResultCompletionRegression};
            ELSE IF EXISTS (
                SELECT 1
                FROM {_tableNames.Sagas}
                WHERE DataType = @DataType AND CorrelationId = @CorrelationId)
                                SET @StoreResult = {StoreResultStaleVersion};
            ELSE
                                SET @StoreResult = {StoreResultMissingSaga};
            """;
        AddBytesParameter(command, "@Data", serialized.Body, MaxSize);
        AddBooleanParameter(command, "@IsCompleted", isCompleted);
        AddDateTimeOffsetParameter(command, "@UpdatedUtc", now);
        AddDateTimeOffsetParameter(command, "@CompletedUtc", isCompleted ? now : null);
        AddStringParameter(command, "@DataType", serialized.DataType, SqlSagaSchema.DataTypeMaxLength);
        AddStringParameter(command, "@CorrelationId", data.CorrelationId, SqlSagaSchema.CorrelationIdMaxLength);
        AddBytesParameter(command, "@ExpectedVersion", expectedVersion, SqlSagaSchema.VersionSize);
        var storeResultParameter = AddOutputParameter(command, "@StoreResult");

        int storeResult;
        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            storeResult = ReadStoreResult(storeResultParameter, typeof(TData), data.CorrelationId);
        }
        catch (SqlException exception)
        {
            throw new SagaPersistenceException(
                $"Failed to store saga data '{typeof(TData).FullName}' with correlation id '{data.CorrelationId}': {exception.Message}",
                exception);
        }

        if (storeResult == StoreResultSuccess)
        {
            return;
        }

        if (storeResult == StoreResultMissingSaga)
        {
            throw new SagaPersistenceException(
                $"Saga data '{typeof(TData).FullName}' with correlation id '{data.CorrelationId}' does not exist.");
        }

        if (storeResult == StoreResultCompletionRegression)
        {
            throw new SagaPersistenceException(
                $"Saga data '{typeof(TData).FullName}' with correlation id '{data.CorrelationId}' is already completed and cannot be marked incomplete.");
        }

        throw new SagaPersistenceException(
            $"Saga data '{typeof(TData).FullName}' with correlation id '{data.CorrelationId}' has a stale version.");
    }

    // SQL exposes rowversion as raw bytes. MiniBus returns it as an opaque base64 token so callers
    // can round-trip it through the public saga persistence contract without depending on SQL types.
    private static string EncodeVersion(byte[] version)
    {
        return Convert.ToBase64String(version);
    }

    // Missing or malformed tokens are caller errors; stale-but-well-formed tokens are handled by
    // the conditional UPDATE and reported as SagaPersistenceException.
    private static byte[] DecodeVersion(string version)
    {
        byte[] decoded;
        try
        {
            decoded = Convert.FromBase64String(version);
        }
        catch (FormatException exception)
        {
            throw new ArgumentException("Saga persistence version must be a valid base64-encoded token.", nameof(version), exception);
        }

        if (decoded.Length != SqlSagaSchema.VersionSize)
        {
            throw new ArgumentException(
            $"Saga persistence version must decode to a SQL rowversion token of {SqlSagaSchema.VersionSize} bytes.",
                nameof(version));
        }

        return decoded;
    }

    private static bool IsDuplicateKey(SqlException exception)
    {
        return exception.Number is 2601 or 2627;
    }

    private static TData CreateSerializedData<TData>(TData data, bool isCompleted)
        where TData : class, ISagaData, new()
    {
        if (data.IsCompleted == isCompleted)
        {
            return data;
        }

        var clone = new TData();
        foreach (var property in GetCloneProperties(typeof(TData)))
        {
            property.SetValue(clone, property.GetValue(data));
        }

        clone.IsCompleted = isCompleted;
        return clone;
    }

    private static PropertyInfo[] GetCloneProperties(Type dataType)
    {
        return ClonePropertiesByType.GetOrAdd(
            dataType,
            static type => type
                .GetProperties()
                .Where(property =>
                    property.CanRead
                    && property.CanWrite
                    && property.GetMethod is not null
                    && !property.GetMethod.IsStatic
                    && property.SetMethod is not null
                    && !property.SetMethod.IsStatic
                    && property.GetIndexParameters().Length == 0)
                .ToArray());
    }

    private static int ReadStoreResult(DbParameter storeResultParameter, Type dataType, string correlationId)
    {
        if (storeResultParameter.Value is null or DBNull)
        {
            throw new SagaPersistenceException(
                $"Failed to store saga data '{dataType.FullName}' with correlation id '{correlationId}': SQL did not return a saga store result.",
                new InvalidOperationException("The SQL saga store result output parameter was not assigned."));
        }

        try
        {
            return Convert.ToInt32(storeResultParameter.Value);
        }
        catch (Exception exception) when (exception is FormatException or InvalidCastException or OverflowException)
        {
            throw new SagaPersistenceException(
                $"Failed to store saga data '{dataType.FullName}' with correlation id '{correlationId}': SQL returned an invalid saga store result.",
                exception);
        }
    }

    private static void AddStringParameter(DbCommand command, string name, string value, int size)
    {
        var parameter = CreateParameter(command, name, value, DbType.String, size);
        if (parameter is SqlParameter sqlParameter)
        {
            sqlParameter.SqlDbType = SqlDbType.NVarChar;
        }

        command.Parameters.Add(parameter);
    }

    private static void AddBytesParameter(DbCommand command, string name, byte[] value, int size)
    {
        var parameter = CreateParameter(command, name, value, DbType.Binary, size);
        if (parameter is SqlParameter sqlParameter)
        {
            sqlParameter.SqlDbType = SqlDbType.VarBinary;
        }

        command.Parameters.Add(parameter);
    }

    private static void AddGuidParameter(DbCommand command, string name, Guid value)
    {
        var parameter = CreateParameter(command, name, value, DbType.Guid);
        if (parameter is SqlParameter sqlParameter)
        {
            sqlParameter.SqlDbType = SqlDbType.UniqueIdentifier;
        }

        command.Parameters.Add(parameter);
    }

    private static void AddBooleanParameter(DbCommand command, string name, bool value)
    {
        var parameter = CreateParameter(command, name, value, DbType.Boolean);
        if (parameter is SqlParameter sqlParameter)
        {
            sqlParameter.SqlDbType = SqlDbType.Bit;
        }

        command.Parameters.Add(parameter);
    }

    private static void AddDateTimeOffsetParameter(DbCommand command, string name, DateTimeOffset? value)
    {
        var parameter = CreateParameter(command, name, value ?? (object)DBNull.Value, DbType.DateTimeOffset);
        if (parameter is SqlParameter sqlParameter)
        {
            sqlParameter.SqlDbType = SqlDbType.DateTimeOffset;
        }

        command.Parameters.Add(parameter);
    }

    private static DbParameter CreateParameter(
        DbCommand command,
        string name,
        object value,
        DbType dbType,
        int? size = null)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = dbType;
        if (size is not null)
        {
            parameter.Size = size.Value;
        }

        parameter.Value = value;
        return parameter;
    }

    private static DbParameter AddOutputParameter(DbCommand command, string name)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Direction = ParameterDirection.Output;
        parameter.DbType = DbType.Int32;
        if (parameter is SqlParameter sqlParameter)
        {
            sqlParameter.SqlDbType = SqlDbType.Int;
        }

        command.Parameters.Add(parameter);
        return parameter;
    }
}
