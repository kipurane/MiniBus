using System.Globalization;
using System.Text.Json;
using MiniBus.Tooling.Core;

namespace MiniBus.Tooling.Cli;

public sealed class MiniBusToolingCliApplication
{
    private const int TableCellMaxLength = 120;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly IMiniBusInboxToolingReader _inboxReader;
    private readonly IMiniBusOutboxToolingReader _outboxReader;
    private readonly IMiniBusSagaToolingReader _sagaReader;
    private readonly IMiniBusTimelineToolingReader _timelineReader;
    private readonly IMiniBusOutboxDrainAction _outboxDrainAction;

    public MiniBusToolingCliApplication(
        IMiniBusInboxToolingReader inboxReader,
        IMiniBusOutboxToolingReader outboxReader,
        IMiniBusSagaToolingReader sagaReader,
        IMiniBusTimelineToolingReader timelineReader,
        IMiniBusOutboxDrainAction outboxDrainAction)
    {
        ArgumentNullException.ThrowIfNull(inboxReader);
        ArgumentNullException.ThrowIfNull(outboxReader);
        ArgumentNullException.ThrowIfNull(sagaReader);
        ArgumentNullException.ThrowIfNull(timelineReader);
        ArgumentNullException.ThrowIfNull(outboxDrainAction);

        _inboxReader = inboxReader;
        _outboxReader = outboxReader;
        _sagaReader = sagaReader;
        _timelineReader = timelineReader;
        _outboxDrainAction = outboxDrainAction;
    }

    public async Task<int> RunAsync(
        MiniBusToolingCliParsedCommand command,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (command.Arguments.Count == 0)
            {
                error.WriteLine("Missing command. Use --help for usage.");
                return 1;
            }

            var format = command.Options.GetValueOrDefault("format") ?? "table";
            var json = string.Equals(format, "json", StringComparison.OrdinalIgnoreCase);

            return command.Arguments[0].ToLowerInvariant() switch
            {
                "inbox" => await RunInboxAsync(command, output, error, json, cancellationToken).ConfigureAwait(false),
                "outbox" => await RunOutboxAsync(command, output, error, json, cancellationToken).ConfigureAwait(false),
                "sagas" => await RunSagasAsync(command, output, error, json, cancellationToken).ConfigureAwait(false),
                "show" => await RunShowAsync(command, output, error, json, cancellationToken).ConfigureAwait(false),
                _ => UnknownCommand(command, error)
            };
        }
        catch (ArgumentException exception)
        {
            error.WriteLine(exception.Message);
            return 1;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            error.WriteLine("MiniBus tooling command failed. Error code: MINIBUS_TOOLING_COMMAND_FAILED.");
            return 1;
        }
    }

    private async Task<int> RunInboxAsync(
        MiniBusToolingCliParsedCommand command,
        TextWriter output,
        TextWriter error,
        bool json,
        CancellationToken cancellationToken)
    {
        if (!HasSubcommand(command, "list"))
        {
            error.WriteLine("Expected command: inbox list");
            return 1;
        }

        var result = await _inboxReader.ListAsync(CreateFilter(command), cancellationToken).ConfigureAwait(false);
        return WriteQueryResult(
            result,
            output,
            error,
            json,
            records => WriteTable(
                output,
                new[] { "Endpoint", "MessageId", "ProcessedUtc", "CorrelationId" },
                records.Select(record => new[]
                {
                    record.EndpointName,
                    record.MessageId,
                    record.ProcessedUtc.ToString("O"),
                    record.CorrelationId ?? string.Empty
                })));
    }

    private async Task<int> RunOutboxAsync(
        MiniBusToolingCliParsedCommand command,
        TextWriter output,
        TextWriter error,
        bool json,
        CancellationToken cancellationToken)
    {
        if (HasSubcommand(command, "drain"))
        {
            var maxBatches = ParsePositiveInt(command.Options.GetValueOrDefault("max-batches"), "max-batches");
            var drainResult = await _outboxDrainAction
                .DrainAsync(new MiniBusOutboxDrainRequest(maxBatches), cancellationToken)
                .ConfigureAwait(false);
            if (!drainResult.IsSupported || !drainResult.Succeeded)
            {
                if (json)
                {
                    WriteObject(drainResult, output, json: true);
                }
                else
                {
                    error.WriteLine(drainResult.Error);
                }

                return drainResult.IsSupported ? 1 : 2;
            }

            if (json)
            {
                WriteObject(drainResult, output, json: true);
            }
            else
            {
                output.WriteLine(drainResult.Message);
            }

            return 0;
        }

        if (!HasSubcommand(command, "list"))
        {
            error.WriteLine("Expected command: outbox list or outbox drain");
            return 1;
        }

        var result = await _outboxReader.ListAsync(CreateFilter(command), cancellationToken).ConfigureAwait(false);
        return WriteQueryResult(
            result,
            output,
            error,
            json,
            records => WriteTable(
                output,
                new[] { "Status", "Endpoint", "IncomingId", "OutgoingId", "Kind", "Attempts", "CreatedUtc" },
                records.Select(record => new[]
                {
                    record.Status.ToString(),
                    record.EndpointName,
                    record.IncomingMessageId,
                    record.OutgoingMessageId,
                    record.OperationKind,
                    record.AttemptCount.ToString(),
                    record.CreatedUtc.ToString("O")
                })));
    }

    private async Task<int> RunSagasAsync(
        MiniBusToolingCliParsedCommand command,
        TextWriter output,
        TextWriter error,
        bool json,
        CancellationToken cancellationToken)
    {
        if (!HasSubcommand(command, "list"))
        {
            error.WriteLine("Expected command: sagas list");
            return 1;
        }

        var result = await _sagaReader.ListAsync(CreateFilter(command), cancellationToken).ConfigureAwait(false);
        return WriteQueryResult(
            result,
            output,
            error,
            json,
            records => WriteTable(
                output,
                new[] { "Status", "DataType", "CorrelationId", "UpdatedUtc", "Version" },
                records.Select(record => new[]
                {
                    record.Status.ToString(),
                    record.DataType,
                    record.CorrelationId,
                    record.UpdatedUtc.ToString("O"),
                    record.Version
                })));
    }

    private async Task<int> RunShowAsync(
        MiniBusToolingCliParsedCommand command,
        TextWriter output,
        TextWriter error,
        bool json,
        CancellationToken cancellationToken)
    {
        if (command.Arguments.Count < 2)
        {
            error.WriteLine("Expected command: show message or show correlation");
            return 1;
        }

        var query = command.Arguments[1].ToLowerInvariant() switch
        {
            "message" => new MiniBusTimelineQuery
            {
                MessageId = RequireOption(command, "message-id"),
                FromUtc = ParseDateTimeOffset(command.Options.GetValueOrDefault("from")),
                ToUtc = ParseDateTimeOffset(command.Options.GetValueOrDefault("to")),
                Limit = ParseOptionalPositiveInt(command.Options.GetValueOrDefault("limit"), "limit")
            },
            "correlation" => new MiniBusTimelineQuery
            {
                CorrelationId = RequireOption(command, "correlation-id"),
                FromUtc = ParseDateTimeOffset(command.Options.GetValueOrDefault("from")),
                ToUtc = ParseDateTimeOffset(command.Options.GetValueOrDefault("to")),
                Limit = ParseOptionalPositiveInt(command.Options.GetValueOrDefault("limit"), "limit")
            },
            _ => throw new ArgumentException("Expected command: show message or show correlation.")
        };

        var timeline = await _timelineReader.ReadAsync(query, cancellationToken).ConfigureAwait(false);
        if (json)
        {
            WriteObject(timeline, output, json: true);
            return 0;
        }

        WriteTable(
            output,
            new[] { "Source", "Kind", "Timestamp", "Title" },
            timeline.Fragments.Select(fragment => new[]
            {
                fragment.Source.ToString(),
                fragment.Kind,
                fragment.Timestamp.ToString("O"),
                fragment.Title
            }));
        var unavailable = timeline.Sources.Where(source => !source.IsAvailable).ToArray();
        if (unavailable.Length > 0)
        {
            output.WriteLine();
            output.WriteLine("Unavailable sources:");
            foreach (var source in unavailable)
            {
                output.WriteLine($"{source.Source}: {source.Reason}");
            }
        }

        return 0;
    }

    private static int WriteQueryResult<T>(
        MiniBusToolingQueryResult<T> result,
        TextWriter output,
        TextWriter error,
        bool json,
        Action<IReadOnlyList<T>> writeTable)
    {
        if (!result.IsSupported)
        {
            error.WriteLine(result.UnsupportedReason);
            return 2;
        }

        if (json)
        {
            WriteObject(result.Records, output, json: true);
            return 0;
        }

        writeTable(result.Records);
        return 0;
    }

    private static void WriteObject(object value, TextWriter output, bool json)
    {
        if (json)
        {
            output.WriteLine(JsonSerializer.Serialize(value, JsonOptions));
            return;
        }

        output.WriteLine(value);
    }

    private static void WriteTable(
        TextWriter output,
        IReadOnlyList<string> headers,
        IEnumerable<IReadOnlyList<string>> rows)
    {
        var sanitizedHeaders = headers.Select(SanitizeTableCell).ToArray();
        output.WriteLine(string.Join(" | ", sanitizedHeaders));
        output.WriteLine(string.Join("-|-", sanitizedHeaders.Select(header => new string('-', Math.Max(3, header.Length)))));
        foreach (var row in rows)
        {
            output.WriteLine(string.Join(" | ", row.Select(SanitizeTableCell)));
        }
    }

    private static string SanitizeTableCell(string value)
    {
        var normalized = value
            .Replace("\r\n", " ", StringComparison.Ordinal)
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Replace("|", "\\|", StringComparison.Ordinal);

        if (normalized.Length <= TableCellMaxLength)
        {
            return normalized;
        }

        const string ellipsis = "...";
        return string.Concat(normalized.AsSpan(0, TableCellMaxLength - ellipsis.Length), ellipsis);
    }

    private static MiniBusToolingQueryFilter CreateFilter(MiniBusToolingCliParsedCommand command)
    {
        return new MiniBusToolingQueryFilter
        {
            EndpointName = command.Options.GetValueOrDefault("endpoint"),
            MessageId = command.Options.GetValueOrDefault("message-id"),
            CorrelationId = command.Options.GetValueOrDefault("correlation-id"),
            Status = command.Options.GetValueOrDefault("status"),
            FromUtc = ParseDateTimeOffset(command.Options.GetValueOrDefault("from")),
            ToUtc = ParseDateTimeOffset(command.Options.GetValueOrDefault("to")),
            Limit = ParseOptionalPositiveInt(command.Options.GetValueOrDefault("limit"), "limit")
        };
    }

    private static bool HasSubcommand(
        MiniBusToolingCliParsedCommand command,
        string name)
    {
        return command.Arguments.Count >= 2
               && string.Equals(command.Arguments[1], name, StringComparison.OrdinalIgnoreCase);
    }

    private static int UnknownCommand(
        MiniBusToolingCliParsedCommand command,
        TextWriter error)
    {
        error.WriteLine($"Unknown command: {string.Join(' ', command.Arguments)}");
        return 1;
    }

    private static string RequireOption(
        MiniBusToolingCliParsedCommand command,
        string name)
    {
        if (!command.Options.TryGetValue(name, out var value)
            || string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"Missing required option: --{name} <value>");
        }

        return value;
    }

    private static int ParsePositiveInt(string? value, string name)
    {
        if (!int.TryParse(value, out var parsed) || parsed <= 0)
        {
            throw new ArgumentException($"--{name} must be a positive integer.");
        }

        return parsed;
    }

    private static int? ParseOptionalPositiveInt(string? value, string name)
    {
        return string.IsNullOrWhiteSpace(value) ? null : ParsePositiveInt(value, name);
    }

    private static DateTimeOffset? ParseDateTimeOffset(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var parsed))
        {
            return parsed;
        }

        throw new ArgumentException(
            $"Invalid date/time value '{value}'. Use an ISO 8601 timestamp such as 2026-05-25T10:00:00Z.");
    }
}
