namespace MiniBus.Core.Auditing;

public sealed record MiniBusAuditClaimCheckReference(
    string Provider,
    string ContainerName,
    string BlobName,
    string PayloadId,
    long Length,
    string? ContentType,
    DateTimeOffset CreatedUtc,
    DateTimeOffset? ExpiresUtc);
