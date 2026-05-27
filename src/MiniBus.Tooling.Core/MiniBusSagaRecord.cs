namespace MiniBus.Tooling.Core;

public sealed record MiniBusSagaRecord(
    Guid Id,
    string DataType,
    string CorrelationId,
    DateTimeOffset CreatedUtc,
    DateTimeOffset UpdatedUtc,
    bool IsCompleted,
    DateTimeOffset? CompletedUtc,
    string Version,
    MiniBusSagaStatus Status);
