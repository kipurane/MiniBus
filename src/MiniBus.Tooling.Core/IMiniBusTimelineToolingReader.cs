namespace MiniBus.Tooling.Core;

public interface IMiniBusTimelineToolingReader
{
    Task<MiniBusMessageTimeline> ReadAsync(
        MiniBusTimelineQuery query,
        CancellationToken cancellationToken = default);
}
