using Azure.Messaging.ServiceBus;
using MiniBus.AzureServiceBus.TransportMessageMapping;
using MiniBus.Core.Auditing;
using MiniBus.Core.ClaimCheck;
using MiniBus.Core.Recoverability;

namespace MiniBus.AzureFunctions.Processing.Pipeline;

internal static class MiniBusAuditRecordFactory
{
    private static readonly IReadOnlyDictionary<string, string> EmptyHeaders =
        new Dictionary<string, string>(StringComparer.Ordinal);

    public static MiniBusAuditRecord Create(
        MiniBusProcessingContext context,
        MiniBusAuditProcessingOutcome outcome,
        string auditId,
        DateTimeOffset auditedUtc,
        string? deadLetterReason = null,
        string? deadLetterDescription = null)
    {
        var headers = new Dictionary<string, string>(context.Headers, StringComparer.Ordinal);
        var messageId = GetRequiredHeaderOrValue(headers, MiniBusHeaderNames.MessageId, context.Message.MessageId);
        var correlationId = GetRequiredHeaderOrValue(headers, MiniBusHeaderNames.CorrelationId, context.Message.CorrelationId);
        var causationId = headers.TryGetValue(MiniBusHeaderNames.CausationId, out var headerCausationId)
            ? headerCausationId
            : null;
        var claimCheck = TryReadClaimCheck(headers);
        var body = ShouldCaptureBody(context, claimCheck) ? context.Body : null;

        return new MiniBusAuditRecord(
            auditId,
            context.Options.EndpointName,
            messageId,
            correlationId,
            causationId,
            context.MessageType?.AssemblyQualifiedName
            ?? GetHeaderValue(headers, MiniBusHeaderNames.MessageType)
            ?? context.Message.Subject,
            outcome,
            auditedUtc,
            GetReceivedUtc(context.Message),
            headers,
            body is null ? MiniBusAuditBodyCaptureMode.None : MiniBusAuditBodyCaptureMode.InlineBody,
            body,
            claimCheck,
            deadLetterReason,
            deadLetterDescription,
            GetRecoverabilityMetadata(headers),
            context.Message.MessageId,
            context.Message.CorrelationId,
            context.Message.Subject,
            context.Message.ContentType,
            context.Message.DeliveryCount);
    }

    private static bool ShouldCaptureBody(
        MiniBusProcessingContext context,
        MiniBusAuditClaimCheckReference? claimCheck)
    {
        return claimCheck is null || context.Options.Audit.CaptureClaimCheckedBodies;
    }

    private static MiniBusAuditClaimCheckReference? TryReadClaimCheck(IReadOnlyDictionary<string, string> headers)
    {
        if (!MiniBusClaimCheckReferenceReader.IsClaimChecked(headers))
        {
            return null;
        }

        var reference = MiniBusClaimCheckReferenceReader.Read(headers);
        return new MiniBusAuditClaimCheckReference(
            reference.Provider,
            reference.ContainerName,
            reference.BlobName,
            reference.PayloadId,
            reference.Length,
            reference.ContentType,
            reference.CreatedUtc,
            reference.ExpiresUtc);
    }

    private static IReadOnlyDictionary<string, string> GetRecoverabilityMetadata(IReadOnlyDictionary<string, string> headers)
    {
        Dictionary<string, string>? recoverabilityMetadata = null;

        foreach (var pair in headers)
        {
            if (!pair.Key.StartsWith("MiniBus.Retry.", StringComparison.Ordinal)
                && !pair.Key.StartsWith("MiniBus.Exception.", StringComparison.Ordinal)
                && !string.Equals(pair.Key, MiniBusRecoverabilityHeaderNames.OriginalMessageId, StringComparison.Ordinal))
            {
                continue;
            }

            recoverabilityMetadata ??= new Dictionary<string, string>(StringComparer.Ordinal);
            recoverabilityMetadata[pair.Key] = pair.Value;
        }

        return recoverabilityMetadata ?? EmptyHeaders;
    }

    private static string? GetHeaderValue(
        IReadOnlyDictionary<string, string> headers,
        string headerName)
    {
        return headers.TryGetValue(headerName, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : null;
    }

    private static string GetRequiredHeaderOrValue(
        IReadOnlyDictionary<string, string> headers,
        string headerName,
        string? fallback)
    {
        var value = GetHeaderValue(headers, headerName);
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        if (!string.IsNullOrWhiteSpace(fallback))
        {
            return fallback;
        }

        throw new InvalidOperationException(
            $"MiniBus audit record cannot be created because required metadata '{headerName}' is missing.");
    }

    private static DateTimeOffset? GetReceivedUtc(ServiceBusReceivedMessage message)
    {
        return message.EnqueuedTime == default
            ? null
            : message.EnqueuedTime;
    }
}
