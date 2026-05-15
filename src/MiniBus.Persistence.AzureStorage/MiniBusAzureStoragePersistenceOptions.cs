using Azure.Storage.Blobs;

namespace MiniBus.Persistence.AzureStorage;

public sealed class MiniBusAzureStoragePersistenceOptions
{
    public string ConnectionString { get; set; } = string.Empty;

    public Func<BlobContainerClient>? BlobContainerClientFactory { get; set; }

    public string ContainerName { get; set; } = "minibus-payloads";

    public string BlobNamePrefix { get; set; } = "payloads";

    public TimeSpan? PayloadRetention { get; set; }

    public Func<DateTimeOffset> UtcNowProvider { get; set; } = () => DateTimeOffset.UtcNow;

    public Func<string> PayloadIdFactory { get; set; } = () => Guid.NewGuid().ToString("N");
}
