namespace MiniBus.Core.Persistence;

public sealed record MiniBusOutboxStoredOperation(
    Guid Id,
    string OutgoingMessageId,
    MiniBusOutboxOperationKind Kind,
    BinaryData Body,
    Type MessageType,
    IReadOnlyDictionary<string, string> Headers,
    DateTimeOffset? DueTime,
    int AttemptCount);
