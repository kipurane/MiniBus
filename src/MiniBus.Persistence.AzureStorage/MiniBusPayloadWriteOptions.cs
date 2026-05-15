namespace MiniBus.Persistence.AzureStorage;

public sealed class MiniBusPayloadWriteOptions
{
    public string? PayloadId { get; set; }

    public string? ContentType { get; set; }

    public DateTimeOffset? ExpiresUtc { get; set; }
}
