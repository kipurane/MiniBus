namespace MiniBus.Core.ClaimCheck;

public sealed class MiniBusClaimCheckPayloadWriteOptions
{
    public string? PayloadId { get; set; }

    public string? ContentType { get; set; }

    public DateTimeOffset? ExpiresUtc { get; set; }
}
