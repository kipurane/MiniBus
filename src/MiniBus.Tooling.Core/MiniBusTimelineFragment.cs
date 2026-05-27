namespace MiniBus.Tooling.Core;

public sealed record MiniBusTimelineFragment(
    MiniBusTimelineSource Source,
    string Kind,
    DateTimeOffset Timestamp,
    string Title,
    IReadOnlyDictionary<string, string> Details);
