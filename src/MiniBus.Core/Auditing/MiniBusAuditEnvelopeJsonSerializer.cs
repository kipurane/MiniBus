using System.Text.Json;
using System.Text.Json.Serialization;

namespace MiniBus.Core.Auditing;

public static class MiniBusAuditEnvelopeJsonSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static BinaryData Serialize(MiniBusAuditRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        var envelope = new MiniBusAuditEnvelope(
            record.AuditId,
            record.EndpointName,
            record.MessageId,
            record.CorrelationId,
            record.CausationId,
            record.MessageType,
            record.Outcome.ToString(),
            record.AuditedUtc,
            record.ReceivedUtc,
            record.Headers,
            record.Body is null
                ? null
                : Convert.ToBase64String(record.Body.ToArray()),
            record.BodyCaptureMode.ToString(),
            record.ClaimCheck,
            record.DeadLetterReason,
            record.DeadLetterDescription,
            record.RecoverabilityMetadata,
            new MiniBusAuditSourceMetadata(
                record.SourceMessageId,
                record.SourceCorrelationId,
                record.SourceSubject,
                record.SourceContentType,
                record.SourceDeliveryCount));

        return BinaryData.FromString(JsonSerializer.Serialize(envelope, Options));
    }

    private sealed record MiniBusAuditEnvelope(
        string AuditId,
        string EndpointName,
        string MessageId,
        string CorrelationId,
        string? CausationId,
        string? MessageType,
        string Outcome,
        DateTimeOffset AuditedUtc,
        DateTimeOffset? ReceivedUtc,
        IReadOnlyDictionary<string, string> Headers,
        string? BodyBase64,
        string BodyCaptureMode,
        MiniBusAuditClaimCheckReference? ClaimCheck,
        string? DeadLetterReason,
        string? DeadLetterDescription,
        IReadOnlyDictionary<string, string> RecoverabilityMetadata,
        MiniBusAuditSourceMetadata Source);

    private sealed record MiniBusAuditSourceMetadata(
        string? MessageId,
        string? CorrelationId,
        string? Subject,
        string? ContentType,
        int? DeliveryCount);
}
