namespace MiniBus.Tooling.Core;

public sealed record MiniBusMessageTimeline(
    MiniBusTimelineQuery Query,
    IReadOnlyList<MiniBusTimelineFragment> Fragments,
    IReadOnlyList<MiniBusTimelineSourceAvailability> Sources);
