using MiniBus.Tooling.Cli;
using MiniBus.Tooling.Core;
using Xunit;

namespace MiniBus.Tooling.Cli.Tests;

public sealed class MiniBusToolingCliApplicationTests
{
    [Fact]
    public void Constructor_RejectsNullDependencies()
    {
        var inbox = new RecordingInboxReader(Array.Empty<MiniBusInboxRecord>());
        var outbox = new RecordingOutboxReader(Array.Empty<MiniBusOutboxRecord>());
        var saga = new RecordingSagaReader(Array.Empty<MiniBusSagaRecord>());
        var timeline = new RecordingTimelineReader(new MiniBusMessageTimeline(
            new MiniBusTimelineQuery { MessageId = "message-1" },
            Array.Empty<MiniBusTimelineFragment>(),
            Array.Empty<MiniBusTimelineSourceAvailability>()));
        var drain = new RecordingOutboxDrainAction(MiniBusOutboxDrainResult.Success(0));

        Assert.Equal("inboxReader", Assert.Throws<ArgumentNullException>(
            () => new MiniBusToolingCliApplication(null!, outbox, saga, timeline, drain)).ParamName);
        Assert.Equal("outboxReader", Assert.Throws<ArgumentNullException>(
            () => new MiniBusToolingCliApplication(inbox, null!, saga, timeline, drain)).ParamName);
        Assert.Equal("sagaReader", Assert.Throws<ArgumentNullException>(
            () => new MiniBusToolingCliApplication(inbox, outbox, null!, timeline, drain)).ParamName);
        Assert.Equal("timelineReader", Assert.Throws<ArgumentNullException>(
            () => new MiniBusToolingCliApplication(inbox, outbox, saga, null!, drain)).ParamName);
        Assert.Equal("outboxDrainAction", Assert.Throws<ArgumentNullException>(
            () => new MiniBusToolingCliApplication(inbox, outbox, saga, timeline, null!)).ParamName);
    }

    [Fact]
    public async Task InboxList_ForwardsFiltersAndWritesTable()
    {
        var inbox = new RecordingInboxReader(new[]
        {
            new MiniBusInboxRecord(
                "Billing",
                "message-1",
                DateTimeOffset.Parse("2026-05-25T10:00:00Z"),
                "correlation-1",
                new Dictionary<string, string>())
        });
        var app = CreateApp(inboxReader: inbox);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await app.RunAsync(
            MiniBusToolingCliParser.Parse(new[]
            {
                "inbox",
                "list",
                "--endpoint",
                "Billing",
                "--message-id",
                "message-1"
            }),
            output,
            error);

        Assert.Equal(0, exitCode);
        Assert.Contains("Endpoint", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("message-1", output.ToString(), StringComparison.Ordinal);
        Assert.Equal("Billing", inbox.LastFilter?.EndpointName);
        Assert.Equal("message-1", inbox.LastFilter?.MessageId);
    }

    [Fact]
    public async Task TableOutput_NormalizesCellsWithoutChangingJsonOutput()
    {
        var inbox = new RecordingInboxReader(new[]
        {
            new MiniBusInboxRecord(
                "Billing|East",
                "message-1\r\ncontinued",
                DateTimeOffset.Parse("2026-05-25T10:00:00Z"),
                new string('c', 130),
                new Dictionary<string, string>())
        });
        var app = CreateApp(inboxReader: inbox);
        using var tableOutput = new StringWriter();
        using var tableError = new StringWriter();
        using var jsonOutput = new StringWriter();
        using var jsonError = new StringWriter();

        var tableExitCode = await app.RunAsync(
            MiniBusToolingCliParser.Parse(new[]
            {
                "inbox",
                "list"
            }),
            tableOutput,
            tableError);
        var jsonExitCode = await app.RunAsync(
            MiniBusToolingCliParser.Parse(new[]
            {
                "inbox",
                "list",
                "--format",
                "json"
            }),
            jsonOutput,
            jsonError);

        Assert.Equal(0, tableExitCode);
        Assert.Equal(0, jsonExitCode);
        Assert.Contains(@"Billing\|East", tableOutput.ToString(), StringComparison.Ordinal);
        Assert.Contains("message-1 continued", tableOutput.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("message-1\r\ncontinued", tableOutput.ToString(), StringComparison.Ordinal);
        Assert.Contains(new string('c', 117) + "...", tableOutput.ToString(), StringComparison.Ordinal);
        Assert.Contains("Billing|East", jsonOutput.ToString(), StringComparison.Ordinal);
        Assert.Contains("message-1\\r\\ncontinued", jsonOutput.ToString(), StringComparison.Ordinal);
        Assert.Contains(new string('c', 130), jsonOutput.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task OutboxList_CanWriteJson()
    {
        var outbox = new RecordingOutboxReader(new[]
        {
            new MiniBusOutboxRecord(
                Guid.NewGuid(),
                "outgoing-1",
                "Billing",
                "message-1",
                "Send",
                "Contracts.CreateInvoice",
                null,
                DateTimeOffset.Parse("2026-05-25T10:00:00Z"),
                null,
                null,
                0,
                null,
                MiniBusOutboxStatus.Pending,
                new Dictionary<string, string>())
        });
        var app = CreateApp(outboxReader: outbox);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await app.RunAsync(
            MiniBusToolingCliParser.Parse(new[]
            {
                "outbox",
                "list",
                "--format",
                "json",
                "--status",
                "Pending"
            }),
            output,
            error);

        Assert.Equal(0, exitCode);
        Assert.Contains("\"outgoingMessageId\"", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("\"status\"", output.ToString(), StringComparison.Ordinal);
        Assert.Equal("Pending", outbox.LastFilter?.Status);
    }

    [Fact]
    public async Task ShowCorrelation_WritesTimelineAndUnavailableSources()
    {
        var timelineReader = new RecordingTimelineReader(new MiniBusMessageTimeline(
            new MiniBusTimelineQuery { CorrelationId = "correlation-1" },
            new[]
            {
                new MiniBusTimelineFragment(
                    MiniBusTimelineSource.Inbox,
                    "processed",
                    DateTimeOffset.Parse("2026-05-25T10:00:00Z"),
                    "Inbox processed message",
                    new Dictionary<string, string>())
            },
            new[]
            {
                new MiniBusTimelineSourceAvailability(
                    MiniBusTimelineSource.Broker,
                    IsAvailable: false,
                    "Broker provider is not configured.")
            }));
        var app = CreateApp(timelineReader: timelineReader);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await app.RunAsync(
            MiniBusToolingCliParser.Parse(new[]
            {
                "show",
                "correlation",
                "--correlation-id",
                "correlation-1"
            }),
            output,
            error);

        Assert.Equal(0, exitCode);
        Assert.Contains("Inbox processed message", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("Unavailable sources", output.ToString(), StringComparison.Ordinal);
        Assert.Equal("correlation-1", timelineReader.LastQuery?.CorrelationId);
    }

    [Fact]
    public async Task OutboxDrain_ForwardsExplicitBounds()
    {
        var action = new RecordingOutboxDrainAction(
            MiniBusOutboxDrainResult.Success(dispatchedCount: 3));
        var app = CreateApp(outboxDrainAction: action);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await app.RunAsync(
            MiniBusToolingCliParser.Parse(new[]
            {
                "outbox",
                "drain",
                "--max-batches",
                "5"
            }),
            output,
            error);

        Assert.Equal(0, exitCode);
        Assert.Equal(5, action.LastRequest?.MaxBatches);
        Assert.Contains("Dispatched 3", output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(MiniBusOutboxDrainResult), output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task OutboxDrainFailure_JsonModeWritesStructuredResultWithoutPlainTextError()
    {
        var action = new RecordingOutboxDrainAction(
            MiniBusOutboxDrainResult.Failure("Transport dispatch failed."));
        var app = CreateApp(outboxDrainAction: action);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await app.RunAsync(
            MiniBusToolingCliParser.Parse(new[]
            {
                "outbox",
                "drain",
                "--max-batches",
                "5",
                "--format",
                "json"
            }),
            output,
            error);

        Assert.Equal(1, exitCode);
        Assert.Contains("\"succeeded\": false", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("\"error\": \"Transport dispatch failed.\"", output.ToString(), StringComparison.Ordinal);
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public async Task UnsupportedReadFilter_ReturnsDistinctExitCode()
    {
        var saga = new RecordingSagaReader(
            Array.Empty<MiniBusSagaRecord>(),
            unsupportedReason: "Saga records do not expose endpoint fields.");
        var app = CreateApp(sagaReader: saga);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await app.RunAsync(
            MiniBusToolingCliParser.Parse(new[]
            {
                "sagas",
                "list",
                "--endpoint",
                "Billing"
            }),
            output,
            error);

        Assert.Equal(2, exitCode);
        Assert.Contains("endpoint", error.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InvalidDateFilter_ReturnsUsageError()
    {
        var app = CreateApp();
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await app.RunAsync(
            MiniBusToolingCliParser.Parse(new[]
            {
                "inbox",
                "list",
                "--from",
                "not-a-date"
            }),
            output,
            error);

        Assert.Equal(1, exitCode);
        Assert.Contains("Invalid date/time value", error.ToString(), StringComparison.Ordinal);
        Assert.Contains("not-a-date", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RuntimeFailure_ReturnsStableErrorWithoutRawExceptionDetails()
    {
        var inbox = new ThrowingInboxReader(
            new InvalidOperationException("Server=secret.example;Password=super-secret"));
        var app = CreateApp(inboxReader: inbox);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await app.RunAsync(
            MiniBusToolingCliParser.Parse(new[]
            {
                "inbox",
                "list"
            }),
            output,
            error);

        Assert.Equal(1, exitCode);
        Assert.Contains("MINIBUS_TOOLING_COMMAND_FAILED", error.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("secret.example", error.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("super-secret", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cancellation_StillFlowsToCaller()
    {
        var inbox = new ThrowingInboxReader(new OperationCanceledException());
        var app = CreateApp(inboxReader: inbox);
        using var output = new StringWriter();
        using var error = new StringWriter();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            app.RunAsync(
                MiniBusToolingCliParser.Parse(new[]
                {
                    "inbox",
                    "list"
                }),
                output,
                error,
                cancellation.Token));
    }

    private static MiniBusToolingCliApplication CreateApp(
        IMiniBusInboxToolingReader? inboxReader = null,
        IMiniBusOutboxToolingReader? outboxReader = null,
        IMiniBusSagaToolingReader? sagaReader = null,
        IMiniBusTimelineToolingReader? timelineReader = null,
        IMiniBusOutboxDrainAction? outboxDrainAction = null)
    {
        return new MiniBusToolingCliApplication(
            inboxReader ?? new RecordingInboxReader(Array.Empty<MiniBusInboxRecord>()),
            outboxReader ?? new RecordingOutboxReader(Array.Empty<MiniBusOutboxRecord>()),
            sagaReader ?? new RecordingSagaReader(Array.Empty<MiniBusSagaRecord>()),
            timelineReader ?? new RecordingTimelineReader(new MiniBusMessageTimeline(
                new MiniBusTimelineQuery { MessageId = "message-1" },
                Array.Empty<MiniBusTimelineFragment>(),
                Array.Empty<MiniBusTimelineSourceAvailability>())),
            outboxDrainAction ?? new RecordingOutboxDrainAction(MiniBusOutboxDrainResult.Success(0)));
    }

    private sealed class RecordingInboxReader : IMiniBusInboxToolingReader
    {
        private readonly IReadOnlyList<MiniBusInboxRecord> _records;

        public RecordingInboxReader(IReadOnlyList<MiniBusInboxRecord> records)
        {
            _records = records;
        }

        public MiniBusToolingQueryFilter? LastFilter { get; private set; }

        public Task<MiniBusToolingQueryResult<MiniBusInboxRecord>> ListAsync(
            MiniBusToolingQueryFilter filter,
            CancellationToken cancellationToken = default)
        {
            LastFilter = filter;
            return Task.FromResult(MiniBusToolingQueryResult<MiniBusInboxRecord>.Success(_records));
        }
    }

    private sealed class RecordingOutboxReader : IMiniBusOutboxToolingReader
    {
        private readonly IReadOnlyList<MiniBusOutboxRecord> _records;

        public RecordingOutboxReader(IReadOnlyList<MiniBusOutboxRecord> records)
        {
            _records = records;
        }

        public MiniBusToolingQueryFilter? LastFilter { get; private set; }

        public Task<MiniBusToolingQueryResult<MiniBusOutboxRecord>> ListAsync(
            MiniBusToolingQueryFilter filter,
            CancellationToken cancellationToken = default)
        {
            LastFilter = filter;
            return Task.FromResult(MiniBusToolingQueryResult<MiniBusOutboxRecord>.Success(_records));
        }
    }

    private sealed class ThrowingInboxReader : IMiniBusInboxToolingReader
    {
        private readonly Exception _exception;

        public ThrowingInboxReader(Exception exception)
        {
            _exception = exception;
        }

        public Task<MiniBusToolingQueryResult<MiniBusInboxRecord>> ListAsync(
            MiniBusToolingQueryFilter filter,
            CancellationToken cancellationToken = default)
        {
            throw _exception;
        }
    }

    private sealed class RecordingSagaReader : IMiniBusSagaToolingReader
    {
        private readonly IReadOnlyList<MiniBusSagaRecord> _records;
        private readonly string? _unsupportedReason;

        public RecordingSagaReader(
            IReadOnlyList<MiniBusSagaRecord> records,
            string? unsupportedReason = null)
        {
            _records = records;
            _unsupportedReason = unsupportedReason;
        }

        public Task<MiniBusToolingQueryResult<MiniBusSagaRecord>> ListAsync(
            MiniBusToolingQueryFilter filter,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                _unsupportedReason is null
                    ? MiniBusToolingQueryResult<MiniBusSagaRecord>.Success(_records)
                    : MiniBusToolingQueryResult<MiniBusSagaRecord>.Unsupported(_unsupportedReason));
        }
    }

    private sealed class RecordingTimelineReader : IMiniBusTimelineToolingReader
    {
        private readonly MiniBusMessageTimeline _timeline;

        public RecordingTimelineReader(MiniBusMessageTimeline timeline)
        {
            _timeline = timeline;
        }

        public MiniBusTimelineQuery? LastQuery { get; private set; }

        public Task<MiniBusMessageTimeline> ReadAsync(
            MiniBusTimelineQuery query,
            CancellationToken cancellationToken = default)
        {
            LastQuery = query;
            return Task.FromResult(_timeline);
        }
    }

    private sealed class RecordingOutboxDrainAction : IMiniBusOutboxDrainAction
    {
        private readonly MiniBusOutboxDrainResult _result;

        public RecordingOutboxDrainAction(MiniBusOutboxDrainResult result)
        {
            _result = result;
        }

        public MiniBusOutboxDrainRequest? LastRequest { get; private set; }

        public Task<MiniBusOutboxDrainResult> DrainAsync(
            MiniBusOutboxDrainRequest request,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(_result);
        }
    }
}
