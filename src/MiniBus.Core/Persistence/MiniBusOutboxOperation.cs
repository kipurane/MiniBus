namespace MiniBus.Core.Persistence;

public sealed record MiniBusOutboxOperation(
    MiniBusOutboxOperationKind Kind,
    object Message,
    Type MessageType,
    IReadOnlyDictionary<string, string> Headers,
    DateTimeOffset? DueTime);
