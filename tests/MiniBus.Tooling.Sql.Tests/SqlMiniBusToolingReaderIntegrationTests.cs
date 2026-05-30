using System.Text.Json;
using MiniBus.Tooling.Core;
using MiniBus.Tooling.Sql;
using Xunit;

namespace MiniBus.Tooling.Sql.Tests;

public sealed class SqlMiniBusToolingReaderIntegrationTests :
    IClassFixture<SqlServerFixture>
{
    private readonly SqlServerFixture _fixture;

    public SqlMiniBusToolingReaderIntegrationTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
    }

    [SqlServerFact]
    public async Task Readers_MapSqlStateAndFilters()
    {
        await using var database = await _fixture.CreateDatabaseAsync();
        await database.InsertInboxAsync("Billing", "message-1", "correlation-1");
        await database.InsertOutboxAsync(
            "Billing",
            "message-1",
            "outgoing-1",
            "Send",
            "Contracts.CreateInvoice",
            "correlation-1",
            dispatchedUtc: null,
            lastError: null);
        await database.InsertSagaAsync("BillingSagaData", "correlation-1", isCompleted: false);
        var reader = database.CreateReader();

        var inbox = await reader.ListAsync(new MiniBusToolingQueryFilter
        {
            EndpointName = "Billing",
            MessageId = "message-1"
        });
        var outbox = await ((IMiniBusOutboxToolingReader)reader).ListAsync(new MiniBusToolingQueryFilter
        {
            CorrelationId = "correlation-1",
            Status = "Pending"
        });
        var sagas = await ((IMiniBusSagaToolingReader)reader).ListAsync(new MiniBusToolingQueryFilter
        {
            CorrelationId = "correlation-1",
            Status = "Active"
        });

        var inboxRecord = Assert.Single(inbox.Records);
        Assert.Equal("Billing", inboxRecord.EndpointName);
        Assert.Equal("message-1", inboxRecord.MessageId);
        Assert.Equal("correlation-1", inboxRecord.CorrelationId);

        var outboxRecord = Assert.Single(outbox.Records);
        Assert.Equal("outgoing-1", outboxRecord.OutgoingMessageId);
        Assert.Equal(MiniBusOutboxStatus.Pending, outboxRecord.Status);
        Assert.Equal("Contracts.CreateInvoice", outboxRecord.MessageType);

        var sagaRecord = Assert.Single(sagas.Records);
        Assert.Equal("BillingSagaData", sagaRecord.DataType);
        Assert.Equal(MiniBusSagaStatus.Active, sagaRecord.Status);
        Assert.NotEmpty(sagaRecord.Version);
    }

    [SqlServerFact]
    public async Task Timeline_AssemblesAvailableSqlFragmentsAndUnavailableProviders()
    {
        await using var database = await _fixture.CreateDatabaseAsync();
        await database.InsertInboxAsync("Billing", "message-1", "correlation-1");
        await database.InsertOutboxAsync(
            "Billing",
            "message-1",
            "outgoing-1",
            "Publish",
            "Contracts.InvoiceCreated",
            "correlation-1",
            dispatchedUtc: DateTimeOffset.UtcNow,
            lastError: null);
        await database.InsertSagaAsync("BillingSagaData", "correlation-1", isCompleted: true);
        var reader = database.CreateReader();

        var timeline = await reader.ReadAsync(new MiniBusTimelineQuery { MessageId = "message-1" });

        Assert.Contains(timeline.Fragments, fragment => fragment.Source == MiniBusTimelineSource.Inbox);
        Assert.Contains(timeline.Fragments, fragment => fragment.Source == MiniBusTimelineSource.Outbox);
        Assert.Contains(timeline.Fragments, fragment => fragment.Source == MiniBusTimelineSource.Saga);
        Assert.Contains(
            timeline.Sources,
            source => source.Source == MiniBusTimelineSource.Broker && !source.IsAvailable);
    }

    [SqlServerFact]
    public async Task Timeline_ReusesSingleSqlConnectionForSqlFragments()
    {
        await using var database = await _fixture.CreateDatabaseAsync();
        await database.InsertInboxAsync("Billing", "message-1", "correlation-1");
        await database.InsertOutboxAsync(
            "Billing",
            "message-1",
            "outgoing-1",
            "Publish",
            "Contracts.InvoiceCreated",
            "correlation-1",
            dispatchedUtc: DateTimeOffset.UtcNow,
            lastError: null);
        await database.InsertSagaAsync("BillingSagaData", "correlation-1", isCompleted: true);
        var connectionCreations = 0;
        var reader = database.CreateReader(() => connectionCreations++);

        var timeline = await reader.ReadAsync(new MiniBusTimelineQuery { MessageId = "message-1" });

        Assert.Equal(1, connectionCreations);
        Assert.Contains(timeline.Fragments, fragment => fragment.Source == MiniBusTimelineSource.Inbox);
        Assert.Contains(timeline.Fragments, fragment => fragment.Source == MiniBusTimelineSource.Outbox);
        Assert.Contains(timeline.Fragments, fragment => fragment.Source == MiniBusTimelineSource.Saga);
    }

    [SqlServerFact]
    public async Task Timeline_DefaultSqlReaderDoesNotAdvertiseUiSource()
    {
        await using var database = await _fixture.CreateDatabaseAsync();
        await database.InsertInboxAsync("Billing", "message-1", "correlation-1");
        var reader = database.CreateReader();

        var timeline = await reader.ReadAsync(new MiniBusTimelineQuery { MessageId = "message-1" });

        var uiSource = Assert.Single(
            timeline.Sources,
            source => source.Source == MiniBusTimelineSource.Ui);
        Assert.False(uiSource.IsAvailable);
        Assert.Contains("UI tooling provider", uiSource.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [SqlServerFact]
    public async Task Timeline_ResolvesCorrelationFromOutboxColumnWhenHeadersAreInvalid()
    {
        await using var database = await _fixture.CreateDatabaseAsync();
        await database.InsertOutboxRawHeadersAsync(
            "Billing",
            "message-1",
            "outgoing-1",
            "{not-json",
            correlationId: "correlation-from-column");
        await database.InsertSagaAsync("BillingSagaData", "correlation-from-column", isCompleted: true);
        var reader = database.CreateReader();

        var timeline = await reader.ReadAsync(new MiniBusTimelineQuery { MessageId = "message-1" });

        Assert.Contains(
            timeline.Fragments,
            fragment => fragment.Source == MiniBusTimelineSource.Saga
                        && fragment.Details["correlationId"] == "correlation-from-column");
        Assert.Contains(
            timeline.Fragments,
            fragment => fragment.Source == MiniBusTimelineSource.Outbox
                        && fragment.Details["correlationId"] == "correlation-from-column");
    }

    [SqlServerFact]
    public async Task TimelineLimitKeepsMostRecentFragmentsThenReturnsChronologicalOrder()
    {
        await using var database = await _fixture.CreateDatabaseAsync();
        var older = DateTimeOffset.UtcNow.AddMinutes(-10);
        var newer = DateTimeOffset.UtcNow;
        await database.InsertInboxAsync(
            "Billing",
            "message-1",
            "correlation-1",
            processedUtc: older);
        await database.InsertOutboxAsync(
            "Billing",
            "message-1",
            "outgoing-new",
            "Publish",
            "Contracts.InvoiceCreated",
            "correlation-1",
            dispatchedUtc: newer,
            lastError: null,
            createdUtc: newer);
        var reader = database.CreateReader();

        var timeline = await reader.ReadAsync(new MiniBusTimelineQuery
        {
            MessageId = "message-1",
            Limit = 1
        });

        var fragment = Assert.Single(timeline.Fragments);
        Assert.Equal(MiniBusTimelineSource.Outbox, fragment.Source);
        Assert.Equal("outgoing-new", fragment.Details["outgoingMessageId"]);
    }

    [SqlServerFact]
    public async Task UnsupportedSagaMessageIdFilter_FailsClearly()
    {
        await using var database = await _fixture.CreateDatabaseAsync();
        var reader = database.CreateReader();

        var result = await ((IMiniBusSagaToolingReader)reader).ListAsync(
            new MiniBusToolingQueryFilter { MessageId = "message-1" });

        Assert.False(result.IsSupported);
        Assert.Contains("message id", result.UnsupportedReason, StringComparison.OrdinalIgnoreCase);
    }

    [SqlServerFact]
    public async Task OutboxFiltersCorrelationAndStatusBeforeApplyingLimit()
    {
        await using var database = await _fixture.CreateDatabaseAsync();
        var now = DateTimeOffset.UtcNow;
        await database.InsertOutboxAsync(
            "Billing",
            "message-new-1",
            "outgoing-new-1",
            "Send",
            "Contracts.CreateInvoice",
            "other-correlation",
            dispatchedUtc: null,
            lastError: null,
            createdUtc: now);
        await database.InsertOutboxAsync(
            "Billing",
            "message-new-2",
            "outgoing-new-2",
            "Send",
            "Contracts.CreateInvoice",
            "other-correlation",
            dispatchedUtc: null,
            lastError: null,
            createdUtc: now.AddSeconds(-1));
        await database.InsertOutboxAsync(
            "Billing",
            "message-old-match",
            "outgoing-old-match",
            "Send",
            "Contracts.CreateInvoice",
            "target-correlation",
            dispatchedUtc: null,
            lastError: null,
            createdUtc: now.AddMinutes(-10));
        var reader = database.CreateReader();

        var result = await ((IMiniBusOutboxToolingReader)reader).ListAsync(
            new MiniBusToolingQueryFilter
            {
                CorrelationId = "target-correlation",
                Status = "Pending",
                Limit = 1
            });

        var record = Assert.Single(result.Records);
        Assert.Equal("outgoing-old-match", record.OutgoingMessageId);
    }

    [SqlServerFact]
    public async Task OutboxCorrelationFilterUsesIndexedColumn()
    {
        await using var database = await _fixture.CreateDatabaseAsync();
        Assert.True(await database.OutboxCorrelationIndexExistsAsync());
        await database.InsertOutboxRawHeadersAsync(
            "Billing",
            "bad-outbox-headers",
            "outgoing-bad-headers",
            "{not-json");
        await database.InsertOutboxAsync(
            "Billing",
            "message-1",
            "outgoing-1",
            "Send",
            "Contracts.CreateInvoice",
            "correlation-1",
            dispatchedUtc: null,
            lastError: null);
        var reader = database.CreateReader();

        var result = await ((IMiniBusOutboxToolingReader)reader).ListAsync(
            new MiniBusToolingQueryFilter
            {
                CorrelationId = "correlation-1"
            });

        var record = Assert.Single(result.Records);
        Assert.Equal("outgoing-1", record.OutgoingMessageId);
    }

    [SqlServerFact]
    public async Task OutboxStatusFiltersTreatWhitespaceLastErrorAsNoError()
    {
        await using var database = await _fixture.CreateDatabaseAsync();
        await database.InsertOutboxAsync(
            "Billing",
            "message-1",
            "outgoing-1",
            "Send",
            "Contracts.CreateInvoice",
            "correlation-1",
            dispatchedUtc: null,
            lastError: "   ");
        var reader = database.CreateReader();

        var pending = await ((IMiniBusOutboxToolingReader)reader).ListAsync(
            new MiniBusToolingQueryFilter
            {
                Status = "Pending"
            });
        var failed = await ((IMiniBusOutboxToolingReader)reader).ListAsync(
            new MiniBusToolingQueryFilter
            {
                Status = "Failed"
            });

        var record = Assert.Single(pending.Records);
        Assert.Equal(MiniBusOutboxStatus.Pending, record.Status);
        Assert.Null(record.LastErrorSummary);
        Assert.Empty(failed.Records);
    }

    [SqlServerFact]
    public async Task SagaFiltersStatusBeforeApplyingLimit()
    {
        await using var database = await _fixture.CreateDatabaseAsync();
        var now = DateTimeOffset.UtcNow;
        await database.InsertSagaAsync(
            "BillingSagaData",
            "active-new-1",
            isCompleted: false,
            updatedUtc: now);
        await database.InsertSagaAsync(
            "BillingSagaData",
            "active-new-2",
            isCompleted: false,
            updatedUtc: now.AddSeconds(-1));
        await database.InsertSagaAsync(
            "BillingSagaData",
            "completed-old-match",
            isCompleted: true,
            updatedUtc: now.AddMinutes(-10));
        var reader = database.CreateReader();

        var result = await ((IMiniBusSagaToolingReader)reader).ListAsync(
            new MiniBusToolingQueryFilter
            {
                Status = "Completed",
                Limit = 1
            });

        var record = Assert.Single(result.Records);
        Assert.Equal("completed-old-match", record.CorrelationId);
        Assert.Equal(MiniBusSagaStatus.Completed, record.Status);
    }

    [SqlServerFact]
    public async Task MalformedHeaders_DoNotFailInboxOrOutboxQueries()
    {
        await using var database = await _fixture.CreateDatabaseAsync();
        await database.InsertInboxRawHeadersAsync(
            "Billing",
            "bad-inbox-headers",
            "{not-json");
        await database.InsertOutboxRawHeadersAsync(
            "Billing",
            "bad-outbox-headers",
            "outgoing-bad-headers",
            "[\"not\", \"an\", \"object\"]");
        await database.InsertOutboxRawHeadersAsync(
            "Billing",
            "malformed-outbox-headers",
            "outgoing-malformed-headers",
            "{not-json");
        await database.InsertOutboxAsync(
            "Billing",
            "good-outbox-headers",
            "outgoing-good-headers",
            "Send",
            "Contracts.CreateInvoice",
            "target-correlation",
            dispatchedUtc: null,
            lastError: null);
        var reader = database.CreateReader();

        var inbox = await reader.ListAsync(new MiniBusToolingQueryFilter
        {
            MessageId = "bad-inbox-headers"
        });
        var outbox = await ((IMiniBusOutboxToolingReader)reader).ListAsync(new MiniBusToolingQueryFilter
        {
            MessageId = "bad-outbox-headers"
        });
        var filteredOutbox = await ((IMiniBusOutboxToolingReader)reader).ListAsync(new MiniBusToolingQueryFilter
        {
            CorrelationId = "target-correlation"
        });

        var inboxRecord = Assert.Single(inbox.Records);
        var outboxRecord = Assert.Single(outbox.Records);
        var filteredOutboxRecord = Assert.Single(filteredOutbox.Records);
        Assert.Empty(inboxRecord.Headers);
        Assert.Empty(outboxRecord.Headers);
        Assert.Equal("outgoing-good-headers", filteredOutboxRecord.OutgoingMessageId);
    }

    [SqlServerFact]
    public async Task NullHeaders_DoNotFailInboxOrOutboxQueries()
    {
        await using var database = await _fixture.CreateDatabaseAsync();
        await database.AllowNullHeadersAsync();
        await database.InsertInboxAsync("Billing", "null-inbox-headers", "correlation-1");
        await database.InsertOutboxAsync(
            "Billing",
            "null-outbox-headers",
            "outgoing-null-headers",
            "Send",
            "Contracts.CreateInvoice",
            "correlation-1",
            dispatchedUtc: null,
            lastError: null);
        await database.SetInboxHeadersNullAsync("Billing", "null-inbox-headers");
        await database.SetOutboxHeadersNullAsync("outgoing-null-headers");
        var reader = database.CreateReader();

        var inbox = await reader.ListAsync(new MiniBusToolingQueryFilter
        {
            MessageId = "null-inbox-headers"
        });
        var outbox = await ((IMiniBusOutboxToolingReader)reader).ListAsync(new MiniBusToolingQueryFilter
        {
            MessageId = "null-outbox-headers"
        });

        var inboxRecord = Assert.Single(inbox.Records);
        var outboxRecord = Assert.Single(outbox.Records);
        Assert.Empty(inboxRecord.Headers);
        Assert.Empty(outboxRecord.Headers);
    }

    [SqlServerFact]
    public async Task Headers_AreSanitizedToSafeSummary()
    {
        await using var database = await _fixture.CreateDatabaseAsync();
        var headersJson = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["MiniBus.CorrelationId"] = "correlation-1",
            ["Authorization"] = "Bearer secret-token",
            ["X-Api-Key"] = "secret-api-key",
            ["CustomerEmail"] = "customer@example.test"
        });
        await database.InsertInboxRawHeadersAsync(
            "Billing",
            "sensitive-inbox-headers",
            headersJson);
        await database.InsertOutboxRawHeadersAsync(
            "Billing",
            "sensitive-outbox-headers",
            "outgoing-sensitive-headers",
            headersJson);
        var reader = database.CreateReader();

        var inbox = await reader.ListAsync(new MiniBusToolingQueryFilter
        {
            MessageId = "sensitive-inbox-headers"
        });
        var outbox = await ((IMiniBusOutboxToolingReader)reader).ListAsync(new MiniBusToolingQueryFilter
        {
            MessageId = "sensitive-outbox-headers"
        });

        var inboxHeaders = Assert.Single(inbox.Records).Headers;
        var outboxHeaders = Assert.Single(outbox.Records).Headers;
        Assert.Single(inboxHeaders);
        Assert.Single(outboxHeaders);
        Assert.Equal("correlation-1", inboxHeaders["MiniBus.CorrelationId"]);
        Assert.Equal("correlation-1", outboxHeaders["MiniBus.CorrelationId"]);
        Assert.DoesNotContain("Authorization", inboxHeaders.Keys);
        Assert.DoesNotContain("Authorization", outboxHeaders.Keys);
        Assert.DoesNotContain("X-Api-Key", inboxHeaders.Keys);
        Assert.DoesNotContain("X-Api-Key", outboxHeaders.Keys);
        Assert.DoesNotContain("CustomerEmail", inboxHeaders.Keys);
        Assert.DoesNotContain("CustomerEmail", outboxHeaders.Keys);
    }

    [SqlServerFact]
    public async Task InboxCorrelationIdFallsBackToHeadersWhenColumnIsBlank()
    {
        await using var database = await _fixture.CreateDatabaseAsync();
        await database.InsertInboxRawHeadersAsync(
            "Billing",
            "blank-correlation-column",
            JsonSerializer.Serialize(new Dictionary<string, string>
            {
                ["MiniBus.CorrelationId"] = "header-correlation"
            }),
            correlationId: "   ");
        var reader = database.CreateReader();

        var inbox = await reader.ListAsync(new MiniBusToolingQueryFilter
        {
            MessageId = "blank-correlation-column"
        });
        var filteredInbox = await reader.ListAsync(new MiniBusToolingQueryFilter
        {
            CorrelationId = "header-correlation"
        });

        var record = Assert.Single(inbox.Records);
        Assert.Equal("header-correlation", record.CorrelationId);
        var filteredRecord = Assert.Single(filteredInbox.Records);
        Assert.Equal("blank-correlation-column", filteredRecord.MessageId);
    }

    [SqlServerFact]
    public async Task LastErrorSummary_RedactsSensitiveValues()
    {
        await using var database = await _fixture.CreateDatabaseAsync();
        await database.InsertOutboxAsync(
            "Billing",
            "failed-message",
            "outgoing-failed-message",
            "Send",
            "Contracts.CreateInvoice",
            "correlation-1",
            dispatchedUtc: null,
            lastError:
                """
                Login failed. Password=super-secret; SharedAccessKey=servicebus-key; Authorization=Bearer auth-token {"clientSecret":"json-secret"} bearer runtime-token
                """);
        var reader = database.CreateReader();

        var outbox = await ((IMiniBusOutboxToolingReader)reader).ListAsync(new MiniBusToolingQueryFilter
        {
            MessageId = "failed-message"
        });

        var record = Assert.Single(outbox.Records);
        Assert.Equal(MiniBusOutboxStatus.Failed, record.Status);
        Assert.NotNull(record.LastErrorSummary);
        Assert.Contains("Password=<redacted>", record.LastErrorSummary);
        Assert.Contains("SharedAccessKey=<redacted>", record.LastErrorSummary);
        Assert.Contains("Authorization=<redacted>", record.LastErrorSummary);
        Assert.Contains("\"clientSecret\":\"<redacted>\"", record.LastErrorSummary);
        Assert.Contains("Bearer <redacted>", record.LastErrorSummary);
        Assert.DoesNotContain("super-secret", record.LastErrorSummary);
        Assert.DoesNotContain("servicebus-key", record.LastErrorSummary);
        Assert.DoesNotContain("auth-token", record.LastErrorSummary);
        Assert.DoesNotContain("json-secret", record.LastErrorSummary);
        Assert.DoesNotContain("runtime-token", record.LastErrorSummary);
    }
}
