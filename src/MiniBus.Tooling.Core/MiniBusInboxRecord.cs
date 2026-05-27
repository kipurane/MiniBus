namespace MiniBus.Tooling.Core;

public sealed record MiniBusInboxRecord(
    string EndpointName,
    string MessageId,
    DateTimeOffset ProcessedUtc,
    string? CorrelationId,
    IReadOnlyDictionary<string, string> Headers);
