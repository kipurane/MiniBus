namespace MiniBus.Core.Persistence;

public sealed record MiniBusInboxMessage(
    string EndpointName,
    string MessageId,
    IReadOnlyDictionary<string, string> Headers,
    DateTimeOffset ProcessedUtc);
