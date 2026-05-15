using System.Text.Json;

namespace MiniBus.Core.ClaimCheck;

public sealed record MiniBusClaimCheckEnvelope(
    string Provider,
    string ContainerName,
    string BlobName,
    string PayloadId,
    long PayloadLength,
    string? ContentType,
    DateTimeOffset CreatedUtc,
    DateTimeOffset? ExpiresUtc)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public BinaryData ToBinaryData()
    {
        return BinaryData.FromString(JsonSerializer.Serialize(this, JsonOptions));
    }

    public static MiniBusClaimCheckEnvelope FromReference(MiniBusClaimCheckPayloadReference reference)
    {
        return new MiniBusClaimCheckEnvelope(
            reference.Provider,
            reference.ContainerName,
            reference.BlobName,
            reference.PayloadId,
            reference.Length,
            reference.ContentType,
            reference.CreatedUtc,
            reference.ExpiresUtc);
    }
}
