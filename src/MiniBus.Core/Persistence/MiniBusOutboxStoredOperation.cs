namespace MiniBus.Core.Persistence;

public sealed record MiniBusOutboxStoredOperation(
    Guid Id,
    MiniBusOutboxOperationKind Kind,
    BinaryData Body,
    Type MessageType,
    IReadOnlyDictionary<string, string> Headers,
    DateTimeOffset? DueTime,
    int AttemptCount);
