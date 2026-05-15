namespace MiniBus.Persistence.AzureStorage;

public sealed record MiniBusPayloadReference(
    string ContainerName,
    string BlobName,
    string PayloadId,
    long Length,
    string? ContentType,
    DateTimeOffset CreatedUtc,
    DateTimeOffset? ExpiresUtc);
