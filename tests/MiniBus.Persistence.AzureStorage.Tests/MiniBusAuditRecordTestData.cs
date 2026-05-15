using MiniBus.Core.Auditing;

namespace MiniBus.Persistence.AzureStorage.Tests;

internal static class MiniBusAuditRecordTestData
{
    public static MiniBusAuditRecord Create(
        BinaryData? body = null,
        DateTimeOffset? auditedUtc = null,
        string? causationId = "causation-1")
    {
        return new MiniBusAuditRecord(
            "audit-1",
            "Billing",
            "message-1",
            "correlation-1",
            causationId,
            "TestMessageType",
            MiniBusAuditProcessingOutcome.Completed,
            auditedUtc ?? new DateTimeOffset(2026, 5, 15, 12, 0, 0, TimeSpan.Zero),
            null,
            new Dictionary<string, string> { ["Header"] = "Value" },
            body is null ? MiniBusAuditBodyCaptureMode.None : MiniBusAuditBodyCaptureMode.InlineBody,
            body,
            null,
            null,
            null,
            new Dictionary<string, string>(),
            "source-message",
            "source-correlation",
            "subject",
            "application/json",
            1);
    }
}
