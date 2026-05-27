namespace MiniBus.Tooling.Core;

public sealed record MiniBusTimelineSourceAvailability(
    MiniBusTimelineSource Source,
    bool IsAvailable,
    string? Reason = null);
