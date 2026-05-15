namespace MiniBus.Core.Auditing;

public sealed record MiniBusAuditRecord(
    string AuditId,
    string EndpointName,
    string MessageId,
    string CorrelationId,
    string? CausationId,
    string? MessageType,
    MiniBusAuditProcessingOutcome Outcome,
    DateTimeOffset AuditedUtc,
    DateTimeOffset? ReceivedUtc,
    IReadOnlyDictionary<string, string> Headers,
    MiniBusAuditBodyCaptureMode BodyCaptureMode,
    BinaryData? Body,
    MiniBusAuditClaimCheckReference? ClaimCheck,
    string? DeadLetterReason,
    string? DeadLetterDescription,
    IReadOnlyDictionary<string, string> RecoverabilityMetadata,
    string? SourceMessageId,
    string? SourceCorrelationId,
    string? SourceSubject,
    string? SourceContentType,
    int? SourceDeliveryCount);
