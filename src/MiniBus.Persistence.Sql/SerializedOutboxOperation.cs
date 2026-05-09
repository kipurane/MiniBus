namespace MiniBus.Persistence.Sql;

public sealed record SerializedOutboxOperation(
    string OperationKind,
    string MessageType,
    byte[] Body,
    string HeadersJson,
    DateTimeOffset? DueTime);
