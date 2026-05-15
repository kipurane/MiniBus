using Azure.Storage.Blobs;
using MiniBus.Core.Auditing;

namespace MiniBus.Persistence.AzureStorage;

public sealed class MiniBusAzureStoragePersistenceOptions
{
    /// <summary>
    /// Gets or sets the Azure Storage connection string used to create default <see cref="BlobContainerClient"/> factories
    /// when caller-provided factories are not configured.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the factory used to obtain the payload <see cref="BlobContainerClient"/>.
    /// </summary>
    /// <remarks>
    /// MiniBus invokes this delegate when constructing <see cref="BlobMiniBusPayloadStore"/>. The default dependency
    /// injection registration adds that store as a singleton, so the delegate is typically evaluated once per service
    /// provider and the resulting client is reused by the store. Callers that manage Azure SDK clients themselves can
    /// return either a new client or a cached client, but reusing a cached <see cref="BlobContainerClient"/> is usually
    /// preferable when sharing Azure SDK configuration and connection pools across components.
    /// </remarks>
    public Func<BlobContainerClient>? BlobContainerClientFactory { get; set; }

    /// <summary>
    /// Gets or sets the factory used to obtain the audit <see cref="BlobContainerClient"/>.
    /// </summary>
    /// <remarks>
    /// MiniBus invokes this delegate when constructing <see cref="BlobMiniBusAuditWriter"/>. The default dependency
    /// injection registration adds that writer as a singleton, so the delegate is typically evaluated once per service
    /// provider and the resulting client is reused for audit writes. Callers that manage Azure SDK clients themselves can
    /// return either a new client or a cached client, but reusing a cached <see cref="BlobContainerClient"/> is usually
    /// preferable when sharing Azure SDK configuration and connection pools across components.
    /// </remarks>
    public Func<BlobContainerClient>? AuditBlobContainerClientFactory { get; set; }

    public string ContainerName { get; set; } = "minibus-payloads";

    public string AuditContainerName { get; set; } = "minibus-audits";

    public string BlobNamePrefix { get; set; } = "payloads";

    public string AuditBlobNamePrefix { get; set; } = "audits";

    public TimeSpan? PayloadRetention { get; set; }

    public TimeSpan? AuditRetention { get; set; }

    public Func<DateTimeOffset> UtcNowProvider { get; set; } = () => DateTimeOffset.UtcNow;

    public Func<string> PayloadIdFactory { get; set; } = () => Guid.NewGuid().ToString("N");

    public Func<string> AuditIdFactory { get; set; } = () => Guid.NewGuid().ToString("N");

    public MiniBusAuditBodyCaptureMode AuditBodyCaptureMode { get; set; } = MiniBusAuditBodyCaptureMode.InlineBody;

    public bool AuditClaimCheckedBodyCaptureEnabled { get; set; }
}
