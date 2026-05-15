namespace MiniBus.Core.ClaimCheck;

public sealed record MiniBusClaimCheckPayloadReference(
    string Provider,
    string ContainerName,
    string BlobName,
    string PayloadId,
    long Length,
    string? ContentType,
    DateTimeOffset CreatedUtc,
    DateTimeOffset? ExpiresUtc);
