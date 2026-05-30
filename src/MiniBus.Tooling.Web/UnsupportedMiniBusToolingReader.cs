using MiniBus.Tooling.Core;

namespace MiniBus.Tooling.Web;

internal sealed class UnsupportedMiniBusToolingReader :
    IMiniBusInboxToolingReader,
    IMiniBusOutboxToolingReader,
    IMiniBusSagaToolingReader,
    IMiniBusTimelineToolingReader
{
    private const string Reason =
        "SQL tooling is not configured. Configure MiniBus:Tooling:Sql:ConnectionString before using MiniBus.Tooling.Web.";

    public Task<MiniBusToolingQueryResult<MiniBusInboxRecord>> ListAsync(
        MiniBusToolingQueryFilter filter,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(MiniBusToolingQueryResult<MiniBusInboxRecord>.Unsupported(Reason));
    }

    Task<MiniBusToolingQueryResult<MiniBusOutboxRecord>> IMiniBusOutboxToolingReader.ListAsync(
        MiniBusToolingQueryFilter filter,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(MiniBusToolingQueryResult<MiniBusOutboxRecord>.Unsupported(Reason));
    }

    Task<MiniBusToolingQueryResult<MiniBusSagaRecord>> IMiniBusSagaToolingReader.ListAsync(
        MiniBusToolingQueryFilter filter,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(MiniBusToolingQueryResult<MiniBusSagaRecord>.Unsupported(Reason));
    }

    public Task<MiniBusMessageTimeline> ReadAsync(
        MiniBusTimelineQuery query,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new MiniBusMessageTimeline(
            query,
            Array.Empty<MiniBusTimelineFragment>(),
            Enum.GetValues<MiniBusTimelineSource>()
                .Select(source => new MiniBusTimelineSourceAvailability(source, false, Reason))
                .ToArray()));
    }
}
