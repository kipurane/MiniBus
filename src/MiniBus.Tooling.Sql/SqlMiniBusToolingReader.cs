using System.Data;
using System.Data.Common;
using System.Collections.ObjectModel;
using System.Text.Json;
using MiniBus.Tooling.Core;

namespace MiniBus.Tooling.Sql;

public sealed class SqlMiniBusToolingReader :
    IMiniBusInboxToolingReader,
    IMiniBusOutboxToolingReader,
    IMiniBusSagaToolingReader,
    IMiniBusTimelineToolingReader
{
    private const int ErrorSummaryMaxLength = 240;
    private const string CorrelationIdHeaderName = "MiniBus.CorrelationId";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly IReadOnlyDictionary<string, string> EmptyHeaders =
        new ReadOnlyDictionary<string, string>(
            new Dictionary<string, string>(StringComparer.Ordinal));

    private readonly MiniBusSqlToolingOptions _options;
    private readonly SqlToolingTableNames _tableNames;

    public SqlMiniBusToolingReader(MiniBusSqlToolingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.DefaultQueryLimit <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options.DefaultQueryLimit),
                options.DefaultQueryLimit,
                "The default SQL tooling query limit must be greater than zero.");
        }

        _options = options;
        _tableNames = new SqlToolingTableNames(options);
    }

    public async Task<MiniBusToolingQueryResult<MiniBusInboxRecord>> ListAsync(
        MiniBusToolingQueryFilter filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        EnsureValidFilter(filter);

        var unsupportedReason = ValidateInboxSupport(filter);
        if (unsupportedReason is not null)
        {
            return MiniBusToolingQueryResult<MiniBusInboxRecord>.Unsupported(
                unsupportedReason);
        }

        await using var connection = _options.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        return await ExecuteInboxAsync(connection, filter, cancellationToken).ConfigureAwait(false);
    }

    private async Task<MiniBusToolingQueryResult<MiniBusInboxRecord>> ExecuteInboxAsync(
        DbConnection connection,
        MiniBusToolingQueryFilter filter,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        AddParameter(command, "@Limit", filter.Limit ?? _options.DefaultQueryLimit);
        var predicates = new List<string>();
        if (!string.IsNullOrWhiteSpace(filter.EndpointName))
        {
            predicates.Add("EndpointName = @EndpointName");
            AddParameter(command, "@EndpointName", filter.EndpointName);
        }

        if (!string.IsNullOrWhiteSpace(filter.MessageId))
        {
            predicates.Add("MessageId = @MessageId");
            AddParameter(command, "@MessageId", filter.MessageId);
        }

        if (!string.IsNullOrWhiteSpace(filter.CorrelationId))
        {
            predicates.Add("""
                (CorrelationId = @CorrelationId
                 OR (NULLIF(LTRIM(RTRIM(CorrelationId)), N'') IS NULL
                     AND ISJSON(HeadersJson) = 1
                     AND JSON_VALUE(HeadersJson, '$."MiniBus.CorrelationId"') = @CorrelationId))
                """);
            AddParameter(command, "@CorrelationId", filter.CorrelationId);
        }

        if (filter.FromUtc is not null)
        {
            predicates.Add("ProcessedUtc >= @FromUtc");
            AddParameter(command, "@FromUtc", filter.FromUtc);
        }

        if (filter.ToUtc is not null)
        {
            predicates.Add("ProcessedUtc <= @ToUtc");
            AddParameter(command, "@ToUtc", filter.ToUtc);
        }

        var whereClause = CreateWhereClause(predicates);
        command.CommandText = $"""
            SELECT TOP (@Limit)
                EndpointName,
                MessageId,
                ProcessedUtc,
                HeadersJson,
                CorrelationId
            FROM {_tableNames.Inbox}
            {whereClause}
            ORDER BY ProcessedUtc DESC;
            """;

        var records = new List<MiniBusInboxRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var headers = DeserializeHeaders(GetStringOrEmpty(reader, 3));
            records.Add(new MiniBusInboxRecord(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetFieldValue<DateTimeOffset>(2),
                GetCorrelationId(reader, 4, headers),
                headers));
        }

        return MiniBusToolingQueryResult<MiniBusInboxRecord>.Success(records);
    }

    async Task<MiniBusToolingQueryResult<MiniBusOutboxRecord>> IMiniBusOutboxToolingReader.ListAsync(
        MiniBusToolingQueryFilter filter,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);

        EnsureValidFilter(filter);

        var unsupportedReason = TryGetOutboxStatus(filter, out var status);
        if (unsupportedReason is not null)
        {
            return MiniBusToolingQueryResult<MiniBusOutboxRecord>.Unsupported(
                unsupportedReason);
        }

        await using var connection = _options.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        return await ExecuteOutboxAsync(connection, filter, status, cancellationToken).ConfigureAwait(false);
    }

    private async Task<MiniBusToolingQueryResult<MiniBusOutboxRecord>> ExecuteOutboxAsync(
        DbConnection connection,
        MiniBusToolingQueryFilter filter,
        MiniBusOutboxStatus? status,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        AddParameter(command, "@Limit", filter.Limit ?? _options.DefaultQueryLimit);
        var predicates = new List<string>();
        if (!string.IsNullOrWhiteSpace(filter.EndpointName))
        {
            predicates.Add("EndpointName = @EndpointName");
            AddParameter(command, "@EndpointName", filter.EndpointName);
        }

        if (!string.IsNullOrWhiteSpace(filter.MessageId))
        {
            predicates.Add("(IncomingMessageId = @MessageId OR OutgoingMessageId = @MessageId)");
            AddParameter(command, "@MessageId", filter.MessageId);
        }

        if (!string.IsNullOrWhiteSpace(filter.CorrelationId))
        {
            predicates.Add("CorrelationId = @CorrelationId");
            AddParameter(command, "@CorrelationId", filter.CorrelationId);
        }

        if (status is not null)
        {
            predicates.Add(status.Value switch
            {
                MiniBusOutboxStatus.Dispatched => "DispatchedUtc IS NOT NULL",
                MiniBusOutboxStatus.Failed => "DispatchedUtc IS NULL AND NULLIF(LTRIM(RTRIM(LastError)), N'') IS NOT NULL",
                MiniBusOutboxStatus.Claimed => "DispatchedUtc IS NULL AND NULLIF(LTRIM(RTRIM(LastError)), N'') IS NULL AND ClaimedUtc IS NOT NULL",
                MiniBusOutboxStatus.Pending => "DispatchedUtc IS NULL AND NULLIF(LTRIM(RTRIM(LastError)), N'') IS NULL AND ClaimedUtc IS NULL",
                _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unsupported outbox status.")
            });
        }

        if (filter.FromUtc is not null)
        {
            predicates.Add("CreatedUtc >= @FromUtc");
            AddParameter(command, "@FromUtc", filter.FromUtc);
        }

        if (filter.ToUtc is not null)
        {
            predicates.Add("CreatedUtc <= @ToUtc");
            AddParameter(command, "@ToUtc", filter.ToUtc);
        }

        var whereClause = CreateWhereClause(predicates);
        command.CommandText = $"""
            SELECT TOP (@Limit)
                Id,
                OutgoingMessageId,
                EndpointName,
                IncomingMessageId,
                OperationKind,
                MessageType,
                DueTime,
                CreatedUtc,
                ClaimedUtc,
                DispatchedUtc,
                AttemptCount,
                LastError,
                HeadersJson,
                CorrelationId
            FROM {_tableNames.Outbox}
            {whereClause}
            ORDER BY CreatedUtc DESC;
            """;

        var records = new List<MiniBusOutboxRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var headers = DeserializeHeaders(GetStringOrEmpty(reader, 12));
            var lastError = GetNonWhiteSpaceString(reader, 11);
            var record = new MiniBusOutboxRecord(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetFieldValue<DateTimeOffset>(6),
                reader.GetFieldValue<DateTimeOffset>(7),
                reader.IsDBNull(8) ? null : reader.GetFieldValue<DateTimeOffset>(8),
                reader.IsDBNull(9) ? null : reader.GetFieldValue<DateTimeOffset>(9),
                reader.GetInt32(10),
                lastError is null ? null : Summarize(lastError),
                DeriveOutboxStatus(
                    reader.IsDBNull(8) ? null : reader.GetFieldValue<DateTimeOffset>(8),
                    reader.IsDBNull(9) ? null : reader.GetFieldValue<DateTimeOffset>(9),
                    lastError),
                headers)
            {
                CorrelationId = GetCorrelationId(reader, 13, headers)
            };

            records.Add(record);
        }

        return MiniBusToolingQueryResult<MiniBusOutboxRecord>.Success(records);
    }

    async Task<MiniBusToolingQueryResult<MiniBusSagaRecord>> IMiniBusSagaToolingReader.ListAsync(
        MiniBusToolingQueryFilter filter,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);

        EnsureValidFilter(filter);

        var unsupportedReason = TryGetSagaStatus(filter, out var status);
        if (unsupportedReason is not null)
        {
            return MiniBusToolingQueryResult<MiniBusSagaRecord>.Unsupported(
                unsupportedReason);
        }

        await using var connection = _options.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        return await ExecuteSagaAsync(connection, filter, status, cancellationToken).ConfigureAwait(false);
    }

    private async Task<MiniBusToolingQueryResult<MiniBusSagaRecord>> ExecuteSagaAsync(
        DbConnection connection,
        MiniBusToolingQueryFilter filter,
        MiniBusSagaStatus? status,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        AddParameter(command, "@Limit", filter.Limit ?? _options.DefaultQueryLimit);
        var predicates = new List<string>();
        if (!string.IsNullOrWhiteSpace(filter.CorrelationId))
        {
            predicates.Add("CorrelationId = @CorrelationId");
            AddParameter(command, "@CorrelationId", filter.CorrelationId);
        }

        if (status is not null)
        {
            predicates.Add(status.Value switch
            {
                MiniBusSagaStatus.Completed => "IsCompleted = 1",
                MiniBusSagaStatus.Active => "IsCompleted = 0",
                _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unsupported saga status.")
            });
        }

        if (filter.FromUtc is not null)
        {
            predicates.Add("UpdatedUtc >= @FromUtc");
            AddParameter(command, "@FromUtc", filter.FromUtc);
        }

        if (filter.ToUtc is not null)
        {
            predicates.Add("UpdatedUtc <= @ToUtc");
            AddParameter(command, "@ToUtc", filter.ToUtc);
        }

        var whereClause = CreateWhereClause(predicates);
        command.CommandText = $"""
            SELECT TOP (@Limit)
                Id,
                DataType,
                CorrelationId,
                CreatedUtc,
                UpdatedUtc,
                IsCompleted,
                CompletedUtc,
                Version
            FROM {_tableNames.Saga}
            {whereClause}
            ORDER BY UpdatedUtc DESC;
            """;

        var records = new List<MiniBusSagaRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var isCompleted = reader.GetBoolean(5);
            var recordStatus = isCompleted ? MiniBusSagaStatus.Completed : MiniBusSagaStatus.Active;
            var record = new MiniBusSagaRecord(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetFieldValue<DateTimeOffset>(3),
                reader.GetFieldValue<DateTimeOffset>(4),
                isCompleted,
                reader.IsDBNull(6) ? null : reader.GetFieldValue<DateTimeOffset>(6),
                Convert.ToBase64String(reader.GetFieldValue<byte[]>(7)),
                recordStatus);

            records.Add(record);
        }

        return MiniBusToolingQueryResult<MiniBusSagaRecord>.Success(records);
    }

    public async Task<MiniBusMessageTimeline> ReadAsync(
        MiniBusTimelineQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var validation = query.Validate();
        if (!validation.IsValid)
        {
            throw new ArgumentException(validation.Error, nameof(query));
        }

        var filter = new MiniBusToolingQueryFilter
        {
            MessageId = query.MessageId,
            CorrelationId = query.CorrelationId,
            FromUtc = query.FromUtc,
            ToUtc = query.ToUtc,
            Limit = query.Limit
        };

        await using var connection = _options.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        var inbox = await ExecuteInboxAsync(connection, filter, cancellationToken).ConfigureAwait(false);
        var outbox = await ExecuteOutboxAsync(connection, filter, status: null, cancellationToken).ConfigureAwait(false);

        var correlationId = query.CorrelationId
                            ?? inbox.Records.Select(record => record.CorrelationId).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
                            ?? outbox.Records
                                .Select(record => record.CorrelationId)
                                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        var sagaFilter = new MiniBusToolingQueryFilter
        {
            CorrelationId = correlationId,
            FromUtc = query.FromUtc,
            ToUtc = query.ToUtc,
            Limit = query.Limit
        };
        var sagas = string.IsNullOrWhiteSpace(correlationId)
            ? MiniBusToolingQueryResult<MiniBusSagaRecord>.Success(Array.Empty<MiniBusSagaRecord>())
            : await ExecuteSagaAsync(connection, sagaFilter, status: null, cancellationToken).ConfigureAwait(false);

        var fragments = inbox.Records.Select(ToFragment)
            .Concat(outbox.Records.Select(ToFragment))
            .Concat(sagas.Records.Select(ToFragment))
            .OrderByDescending(fragment => fragment.Timestamp)
            .Take(query.Limit ?? _options.DefaultQueryLimit)
            .OrderBy(fragment => fragment.Timestamp)
            .ToArray();

        return new MiniBusMessageTimeline(
            query,
            fragments,
            CreateSourceAvailability());
    }

    private static MiniBusTimelineFragment ToFragment(MiniBusInboxRecord record)
    {
        return new MiniBusTimelineFragment(
            MiniBusTimelineSource.Inbox,
            "processed",
            record.ProcessedUtc,
            $"Inbox processed {record.MessageId}",
            new Dictionary<string, string>
            {
                ["endpoint"] = record.EndpointName,
                ["messageId"] = record.MessageId,
                ["correlationId"] = record.CorrelationId ?? string.Empty
            });
    }

    private static MiniBusTimelineFragment ToFragment(MiniBusOutboxRecord record)
    {
        return new MiniBusTimelineFragment(
            MiniBusTimelineSource.Outbox,
            record.Status.ToString(),
            record.DispatchedUtc ?? record.ClaimedUtc ?? record.CreatedUtc,
            $"Outbox {record.OperationKind} {record.MessageType}",
            new Dictionary<string, string>
            {
                ["id"] = record.Id.ToString("D"),
                ["endpoint"] = record.EndpointName,
                ["incomingMessageId"] = record.IncomingMessageId,
                ["outgoingMessageId"] = record.OutgoingMessageId,
                ["correlationId"] = record.CorrelationId ?? string.Empty,
                ["status"] = record.Status.ToString()
            });
    }

    private static MiniBusTimelineFragment ToFragment(MiniBusSagaRecord record)
    {
        return new MiniBusTimelineFragment(
            MiniBusTimelineSource.Saga,
            record.Status.ToString(),
            record.CompletedUtc ?? record.UpdatedUtc,
            $"Saga {record.DataType}",
            new Dictionary<string, string>
            {
                ["id"] = record.Id.ToString("D"),
                ["correlationId"] = record.CorrelationId,
                ["status"] = record.Status.ToString(),
                ["version"] = record.Version
            });
    }

    private static IReadOnlyList<MiniBusTimelineSourceAvailability> CreateSourceAvailability()
    {
        return new[]
        {
            new MiniBusTimelineSourceAvailability(MiniBusTimelineSource.Inbox, true),
            new MiniBusTimelineSourceAvailability(MiniBusTimelineSource.Outbox, true),
            new MiniBusTimelineSourceAvailability(MiniBusTimelineSource.Saga, true),
            new MiniBusTimelineSourceAvailability(MiniBusTimelineSource.Broker, false, "Azure Service Bus tooling provider is not configured."),
            new MiniBusTimelineSourceAvailability(MiniBusTimelineSource.Logs, false, "Structured log tooling provider is not configured."),
            new MiniBusTimelineSourceAvailability(MiniBusTimelineSource.Traces, false, "Trace tooling provider is not configured."),
            new MiniBusTimelineSourceAvailability(MiniBusTimelineSource.Audit, false, "Audit tooling provider is not configured."),
            new MiniBusTimelineSourceAvailability(MiniBusTimelineSource.Ui, false, "The first tooling increment does not include a UI.")
        };
    }

    private static void EnsureValidFilter(MiniBusToolingQueryFilter filter)
    {
        var validation = filter.Validate();
        if (!validation.IsValid)
        {
            throw new ArgumentException(validation.Error, nameof(filter));
        }
    }

    private static string? ValidateInboxSupport(MiniBusToolingQueryFilter filter)
    {
        return !string.IsNullOrWhiteSpace(filter.Status)
               && !StringEquals(filter.Status, "Processed")
            ? "Inbox records support only the 'Processed' status filter."
            : null;
    }

    private static string? TryGetOutboxStatus(
        MiniBusToolingQueryFilter filter,
        out MiniBusOutboxStatus? status)
    {
        status = null;
        if (string.IsNullOrWhiteSpace(filter.Status))
        {
            return null;
        }

        if (Enum.TryParse(filter.Status, ignoreCase: true, out MiniBusOutboxStatus parsedStatus))
        {
            status = parsedStatus;
            return null;
        }

        return "Outbox records support Pending, Claimed, Dispatched, and Failed status filters.";
    }

    private static string? TryGetSagaStatus(
        MiniBusToolingQueryFilter filter,
        out MiniBusSagaStatus? status)
    {
        status = null;
        if (!string.IsNullOrWhiteSpace(filter.EndpointName)
            || !string.IsNullOrWhiteSpace(filter.MessageId))
        {
            return "Saga records do not expose endpoint or message id fields.";
        }

        if (string.IsNullOrWhiteSpace(filter.Status))
        {
            return null;
        }

        if (Enum.TryParse(filter.Status, ignoreCase: true, out MiniBusSagaStatus parsedStatus))
        {
            status = parsedStatus;
            return null;
        }

        return "Saga records support Active and Completed status filters.";
    }

    private static string CreateWhereClause(IReadOnlyCollection<string> predicates)
    {
        return predicates.Count == 0
            ? string.Empty
            : $"WHERE {string.Join($"{Environment.NewLine}  AND ", predicates)}";
    }

    private static void AddParameter(DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static string GetStringOrEmpty(DbDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal);
    }

    private static string? GetNonWhiteSpaceString(DbDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal))
        {
            return null;
        }

        var value = reader.GetString(ordinal);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static IReadOnlyDictionary<string, string> DeserializeHeaders(string headersJson)
    {
        if (string.IsNullOrWhiteSpace(headersJson))
        {
            return EmptyHeaders;
        }

        try
        {
            var headers = JsonSerializer.Deserialize<Dictionary<string, string>>(headersJson, JsonOptions);
            return SanitizeHeaders(headers);
        }
        catch (JsonException)
        {
            return EmptyHeaders;
        }
    }

    private static IReadOnlyDictionary<string, string> SanitizeHeaders(
        IReadOnlyDictionary<string, string>? headers)
    {
        if (headers is null)
        {
            return EmptyHeaders;
        }

        if (!headers.TryGetValue(CorrelationIdHeaderName, out var correlationId)
            || string.IsNullOrWhiteSpace(correlationId))
        {
            return EmptyHeaders;
        }

        return new ReadOnlyDictionary<string, string>(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [CorrelationIdHeaderName] = correlationId
            });
    }

    private static string? GetHeader(
        IReadOnlyDictionary<string, string> headers,
        string name)
    {
        return headers.TryGetValue(name, out var value) ? value : null;
    }

    private static string? GetCorrelationId(
        DbDataReader reader,
        int ordinal,
        IReadOnlyDictionary<string, string> headers)
    {
        if (!reader.IsDBNull(ordinal))
        {
            var value = reader.GetString(ordinal);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return GetHeader(headers, CorrelationIdHeaderName);
    }

    private static MiniBusOutboxStatus DeriveOutboxStatus(
        DateTimeOffset? claimedUtc,
        DateTimeOffset? dispatchedUtc,
        string? lastError)
    {
        if (dispatchedUtc is not null)
        {
            return MiniBusOutboxStatus.Dispatched;
        }

        if (!string.IsNullOrWhiteSpace(lastError))
        {
            return MiniBusOutboxStatus.Failed;
        }

        return claimedUtc is null ? MiniBusOutboxStatus.Pending : MiniBusOutboxStatus.Claimed;
    }

    private static string Summarize(string value)
    {
        return SqlToolingTextRedactor.RedactAndTruncate(value, ErrorSummaryMaxLength);
    }

    private static bool StringEquals(string? left, string? right)
    {
        return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }
}
