using MiniBus.Tooling.Core;
using Xunit;

namespace MiniBus.Tooling.Core.Tests;

public sealed class ToolingCoreTests
{
    [Fact]
    public void QueryFilter_RejectsInvalidTimeWindow()
    {
        var filter = new MiniBusToolingQueryFilter
        {
            FromUtc = new DateTimeOffset(2026, 5, 25, 12, 0, 0, TimeSpan.Zero),
            ToUtc = new DateTimeOffset(2026, 5, 25, 11, 0, 0, TimeSpan.Zero)
        };

        var result = filter.Validate();

        Assert.False(result.IsValid);
        Assert.Contains("time window", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void QueryFilter_RejectsNonPositiveLimit()
    {
        var filter = new MiniBusToolingQueryFilter { Limit = 0 };

        var result = filter.Validate();

        Assert.False(result.IsValid);
        Assert.Contains("Limit", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void TimelineQuery_RequiresOneIdentifier()
    {
        var none = new MiniBusTimelineQuery();
        var both = new MiniBusTimelineQuery
        {
            MessageId = "message-1",
            CorrelationId = "correlation-1"
        };

        Assert.False(none.Validate().IsValid);
        Assert.False(both.Validate().IsValid);
    }

    [Fact]
    public void QueryResult_CanRepresentUnsupportedFilters()
    {
        var result = MiniBusToolingQueryResult<MiniBusInboxRecord>.Unsupported(
            "Inbox records do not support this filter.");

        Assert.False(result.IsSupported);
        Assert.Empty(result.Records);
        Assert.Equal("Inbox records do not support this filter.", result.UnsupportedReason);
    }

    [Fact]
    public void OutboxStatuses_CoverFirstToolingStates()
    {
        var statuses = Enum.GetNames<MiniBusOutboxStatus>();

        Assert.Contains(nameof(MiniBusOutboxStatus.Pending), statuses);
        Assert.Contains(nameof(MiniBusOutboxStatus.Claimed), statuses);
        Assert.Contains(nameof(MiniBusOutboxStatus.Dispatched), statuses);
        Assert.Contains(nameof(MiniBusOutboxStatus.Failed), statuses);
    }

    [Fact]
    public void TimelineFragments_CanBeOrderedByTimestamp()
    {
        var first = new MiniBusTimelineFragment(
            MiniBusTimelineSource.Inbox,
            "processed",
            new DateTimeOffset(2026, 5, 25, 10, 0, 0, TimeSpan.Zero),
            "Inbox processed message",
            new Dictionary<string, string>());
        var second = new MiniBusTimelineFragment(
            MiniBusTimelineSource.Outbox,
            "created",
            new DateTimeOffset(2026, 5, 25, 11, 0, 0, TimeSpan.Zero),
            "Outbox operation created",
            new Dictionary<string, string>());

        var ordered = new[] { second, first }
            .OrderBy(fragment => fragment.Timestamp)
            .ToArray();

        Assert.Same(first, ordered[0]);
        Assert.Same(second, ordered[1]);
    }

    [Fact]
    public void DrainRequest_RequiresPositiveBatchBound()
    {
        var request = new MiniBusOutboxDrainRequest(0);

        var result = request.Validate();

        Assert.False(result.IsValid);
        Assert.Contains("MaxBatches", result.Error, StringComparison.Ordinal);
    }
}
