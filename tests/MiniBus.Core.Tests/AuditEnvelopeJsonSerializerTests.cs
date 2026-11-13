using System.Text.Json;
using MiniBus.Core.Auditing;
using Xunit;

namespace MiniBus.Core.Tests;

public sealed class AuditEnvelopeJsonSerializerTests
{
    [Fact]
    public void Serialize_ProducesValidJsonWithAllRequiredFields()
    {
        var auditedUtc = new DateTimeOffset(2026, 5, 15, 10, 0, 0, TimeSpan.Zero);
        var receivedUtc = new DateTimeOffset(2026, 5, 15, 9, 59, 59, TimeSpan.Zero);
        var record = new MiniBusAuditRecord(
            AuditId: "audit-1",
            EndpointName: "Billing",
            MessageId: "message-1",
            CorrelationId: "correlation-1",
            CausationId: "causation-1",
            MessageType: "BillingCommand",
            Outcome: MiniBusAuditProcessingOutcome.Completed,
            AuditedUtc: auditedUtc,
            ReceivedUtc: receivedUtc,
            Headers: new Dictionary<string, string>(StringComparer.Ordinal) { ["custom"] = "value" },
            BodyCaptureMode: MiniBusAuditBodyCaptureMode.InlineBody,
            Body: BinaryData.FromString("body-content"),
            ClaimCheck: null,
            DeadLetterReason: null,
            DeadLetterDescription: null,
            RecoverabilityMetadata: new Dictionary<string, string>(StringComparer.Ordinal),
            SourceMessageId: "source-message-1",
            SourceCorrelationId: "source-correlation-1",
            SourceSubject: "source-subject",
            SourceContentType: "application/json",
            SourceDeliveryCount: 1);

        var result = MiniBusAuditEnvelopeJsonSerializer.Serialize(record);

        var json = result.ToString();
        Assert.NotEmpty(json);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("audit-1", root.GetProperty("auditId").GetString());
        Assert.Equal("Billing", root.GetProperty("endpointName").GetString());
        Assert.Equal("message-1", root.GetProperty("messageId").GetString());
        Assert.Equal("correlation-1", root.GetProperty("correlationId").GetString());
        Assert.Equal("causation-1", root.GetProperty("causationId").GetString());
        Assert.Equal("BillingCommand", root.GetProperty("messageType").GetString());
        Assert.Equal("Completed", root.GetProperty("outcome").GetString());
        Assert.Equal(Convert.ToBase64String("body-content"u8.ToArray()), root.GetProperty("bodyBase64").GetString());
        Assert.Equal("InlineBody", root.GetProperty("bodyCaptureMode").GetString());

        var source = root.GetProperty("source");
        Assert.Equal("source-message-1", source.GetProperty("messageId").GetString());
        Assert.Equal("source-correlation-1", source.GetProperty("correlationId").GetString());
        Assert.Equal("source-subject", source.GetProperty("subject").GetString());
        Assert.Equal("application/json", source.GetProperty("contentType").GetString());
        Assert.Equal(1, source.GetProperty("deliveryCount").GetInt32());
    }

    [Fact]
    public void Serialize_OmitsNullOptionalFields()
    {
        var record = new MiniBusAuditRecord(
            AuditId: "audit-1",
            EndpointName: "Billing",
            MessageId: "message-1",
            CorrelationId: "correlation-1",
            CausationId: null,
            MessageType: null,
            Outcome: MiniBusAuditProcessingOutcome.Completed,
            AuditedUtc: DateTimeOffset.UtcNow,
            ReceivedUtc: null,
            Headers: new Dictionary<string, string>(StringComparer.Ordinal),
            BodyCaptureMode: MiniBusAuditBodyCaptureMode.None,
            Body: null,
            ClaimCheck: null,
            DeadLetterReason: null,
            DeadLetterDescription: null,
            RecoverabilityMetadata: new Dictionary<string, string>(StringComparer.Ordinal),
            SourceMessageId: null,
            SourceCorrelationId: null,
            SourceSubject: null,
            SourceContentType: null,
            SourceDeliveryCount: null);

        var result = MiniBusAuditEnvelopeJsonSerializer.Serialize(record);

        var json = result.ToString();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal(JsonValueKind.Undefined, root.TryGetProperty("causationId", out _) ? JsonValueKind.String : JsonValueKind.Undefined);
        Assert.False(root.TryGetProperty("bodyBase64", out _));
        Assert.False(root.TryGetProperty("receivedUtc", out _));
        Assert.False(root.TryGetProperty("deadLetterReason", out _));
        Assert.False(root.TryGetProperty("deadLetterDescription", out _));
    }

    [Fact]
    public void Serialize_IncludesClaimCheckWhenPresent()
    {
        var claimCheckRef = new MiniBusAuditClaimCheckReference(
            Provider: "azure-blob-storage",
            ContainerName: "payloads",
            BlobName: "payload.bin",
            PayloadId: "payload-1",
            Length: 512,
            ContentType: "application/json",
            CreatedUtc: new DateTimeOffset(2026, 5, 15, 12, 0, 0, TimeSpan.Zero),
            ExpiresUtc: null);

        var record = new MiniBusAuditRecord(
            AuditId: "audit-1",
            EndpointName: "Billing",
            MessageId: "message-1",
            CorrelationId: "correlation-1",
            CausationId: null,
            MessageType: null,
            Outcome: MiniBusAuditProcessingOutcome.Completed,
            AuditedUtc: DateTimeOffset.UtcNow,
            ReceivedUtc: null,
            Headers: new Dictionary<string, string>(StringComparer.Ordinal),
            BodyCaptureMode: MiniBusAuditBodyCaptureMode.None,
            Body: null,
            ClaimCheck: claimCheckRef,
            DeadLetterReason: null,
            DeadLetterDescription: null,
            RecoverabilityMetadata: new Dictionary<string, string>(StringComparer.Ordinal),
            SourceMessageId: null,
            SourceCorrelationId: null,
            SourceSubject: null,
            SourceContentType: null,
            SourceDeliveryCount: null);

        var result = MiniBusAuditEnvelopeJsonSerializer.Serialize(record);

        var json = result.ToString();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("claimCheck", out var claimCheck));
        Assert.Equal("azure-blob-storage", claimCheck.GetProperty("provider").GetString());
        Assert.Equal("payloads", claimCheck.GetProperty("containerName").GetString());
        Assert.Equal("payload-1", claimCheck.GetProperty("payloadId").GetString());
        Assert.Equal(512, claimCheck.GetProperty("length").GetInt64());
    }

    [Fact]
    public void Serialize_IncludesDeadLetterFieldsWhenPresent()
    {
        var record = new MiniBusAuditRecord(
            AuditId: "audit-1",
            EndpointName: "Billing",
            MessageId: "message-1",
            CorrelationId: "correlation-1",
            CausationId: null,
            MessageType: null,
            Outcome: MiniBusAuditProcessingOutcome.DeadLettered,
            AuditedUtc: DateTimeOffset.UtcNow,
            ReceivedUtc: null,
            Headers: new Dictionary<string, string>(StringComparer.Ordinal),
            BodyCaptureMode: MiniBusAuditBodyCaptureMode.None,
            Body: null,
            ClaimCheck: null,
            DeadLetterReason: "retries exhausted",
            DeadLetterDescription: "ExceptionType=System.InvalidOperationException",
            RecoverabilityMetadata: new Dictionary<string, string>(StringComparer.Ordinal),
            SourceMessageId: null,
            SourceCorrelationId: null,
            SourceSubject: null,
            SourceContentType: null,
            SourceDeliveryCount: null);

        var result = MiniBusAuditEnvelopeJsonSerializer.Serialize(record);

        var json = result.ToString();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("retries exhausted", root.GetProperty("deadLetterReason").GetString());
        Assert.Equal("ExceptionType=System.InvalidOperationException", root.GetProperty("deadLetterDescription").GetString());
    }

    [Fact]
    public void Serialize_ProducesCompactJsonWithCamelCaseProperties()
    {
        var record = new MiniBusAuditRecord(
            AuditId: "audit-1",
            EndpointName: "Billing",
            MessageId: "message-1",
            CorrelationId: "correlation-1",
            CausationId: null,
            MessageType: null,
            Outcome: MiniBusAuditProcessingOutcome.Completed,
            AuditedUtc: DateTimeOffset.UtcNow,
            ReceivedUtc: null,
            Headers: new Dictionary<string, string>(StringComparer.Ordinal),
            BodyCaptureMode: MiniBusAuditBodyCaptureMode.None,
            Body: null,
            ClaimCheck: null,
            DeadLetterReason: null,
            DeadLetterDescription: null,
            RecoverabilityMetadata: new Dictionary<string, string>(StringComparer.Ordinal),
            SourceMessageId: null,
            SourceCorrelationId: null,
            SourceSubject: null,
            SourceContentType: null,
            SourceDeliveryCount: null);

        var result = MiniBusAuditEnvelopeJsonSerializer.Serialize(record);

        var json = result.ToString();
        Assert.DoesNotContain("\n", json, StringComparison.Ordinal);
        Assert.DoesNotContain("  ", json, StringComparison.Ordinal);
        Assert.Contains("\"auditId\"", json, StringComparison.Ordinal);
        Assert.Contains("\"endpointName\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Serialize_ThrowsWhenRecordIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => MiniBusAuditEnvelopeJsonSerializer.Serialize(null!));
    }
}
