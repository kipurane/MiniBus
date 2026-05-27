namespace MiniBus.Tooling.Core;

public sealed record MiniBusOutboxRecord(
    Guid Id,
    string OutgoingMessageId,
    string EndpointName,
    string IncomingMessageId,
    string OperationKind,
    string MessageType,
    DateTimeOffset? DueTime,
    DateTimeOffset CreatedUtc,
    DateTimeOffset? ClaimedUtc,
    DateTimeOffset? DispatchedUtc,
    int AttemptCount,
    string? LastErrorSummary,
    MiniBusOutboxStatus Status,
    IReadOnlyDictionary<string, string> Headers)
{
    public string? CorrelationId { get; init; }
}
