using System.Globalization;

namespace MiniBus.Core.ClaimCheck;

public static class MiniBusClaimCheckReferenceReader
{
    public static bool IsClaimChecked(IReadOnlyDictionary<string, string> headers)
    {
        ArgumentNullException.ThrowIfNull(headers);

        return headers.TryGetValue(MiniBusClaimCheckHeaderNames.Enabled, out var enabled)
               && bool.TryParse(enabled, out var parsed)
               && parsed;
    }

    public static MiniBusClaimCheckPayloadReference Read(IReadOnlyDictionary<string, string> headers)
    {
        ArgumentNullException.ThrowIfNull(headers);

        var provider = GetRequired(headers, MiniBusClaimCheckHeaderNames.Provider);
        var containerName = GetRequired(headers, MiniBusClaimCheckHeaderNames.ContainerName);
        var blobName = GetRequired(headers, MiniBusClaimCheckHeaderNames.BlobName);
        var payloadId = GetRequired(headers, MiniBusClaimCheckHeaderNames.PayloadId);
        var payloadLengthValue = GetRequired(headers, MiniBusClaimCheckHeaderNames.PayloadLength);
        var createdUtcValue = GetRequired(headers, MiniBusClaimCheckHeaderNames.CreatedUtc);

        if (!long.TryParse(payloadLengthValue, NumberStyles.None, CultureInfo.InvariantCulture, out var payloadLength)
            || payloadLength < 0)
        {
            throw new MiniBusInvalidClaimCheckReferenceException(
                $"MiniBus claim-check payload length '{payloadLengthValue}' is invalid.");
        }

        if (!DateTimeOffset.TryParse(
                createdUtcValue,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var createdUtc))
        {
            throw new MiniBusInvalidClaimCheckReferenceException(
                $"MiniBus claim-check created UTC value '{createdUtcValue}' is invalid.");
        }

        DateTimeOffset? expiresUtc = null;
        if (headers.TryGetValue(MiniBusClaimCheckHeaderNames.ExpiresUtc, out var expiresUtcValue)
            && !string.IsNullOrWhiteSpace(expiresUtcValue))
        {
            if (!DateTimeOffset.TryParse(
                    expiresUtcValue,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out var parsedExpiresUtc))
            {
                throw new MiniBusInvalidClaimCheckReferenceException(
                    $"MiniBus claim-check expires UTC value '{expiresUtcValue}' is invalid.");
            }

            expiresUtc = parsedExpiresUtc;
        }

        headers.TryGetValue(MiniBusClaimCheckHeaderNames.ContentType, out var contentType);

        return new MiniBusClaimCheckPayloadReference(
            provider,
            containerName,
            blobName,
            payloadId,
            payloadLength,
            contentType,
            createdUtc,
            expiresUtc);
    }

    private static string GetRequired(IReadOnlyDictionary<string, string> headers, string name)
    {
        if (!headers.TryGetValue(name, out var value) || string.IsNullOrWhiteSpace(value))
        {
            throw new MiniBusInvalidClaimCheckReferenceException(
                $"MiniBus claim-check header '{name}' is missing or empty.");
        }

        return value;
    }
}
