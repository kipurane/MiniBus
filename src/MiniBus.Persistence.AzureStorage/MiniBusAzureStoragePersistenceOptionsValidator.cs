using Azure.Storage.Blobs;

namespace MiniBus.Persistence.AzureStorage;

public static class MiniBusAzureStoragePersistenceOptionsValidator
{
    public static void Validate(MiniBusAzureStoragePersistenceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        ValidatePayloadStorage(options);
        ValidateCommon(options);
    }

    public static void ValidateAudit(MiniBusAzureStoragePersistenceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.AuditBlobContainerClientFactory is null)
        {
            throw new InvalidOperationException(
                "MiniBus Azure Storage audit writing requires a Blob Storage connection string or audit BlobContainerClient factory.");
        }

        ValidateContainerName(
            options.AuditContainerName,
            "MiniBus Azure Storage audit container name cannot be empty.",
            "MiniBus Azure Storage audit container name cannot contain whitespace.");

        if (options.AuditRetention is { } retention && retention <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("MiniBus Azure Storage audit retention must be greater than zero.");
        }

        if (options.AuditIdFactory is null)
        {
            throw new InvalidOperationException("MiniBus Azure Storage AuditIdFactory cannot be null.");
        }

        ValidateCommon(options);
    }

    private static void ValidatePayloadStorage(MiniBusAzureStoragePersistenceOptions options)
    {
        if (options.BlobContainerClientFactory is null)
        {
            throw new InvalidOperationException(
                "MiniBus Azure Storage persistence requires a Blob Storage connection string or BlobContainerClient factory.");
        }

        ValidateContainerName(
            options.ContainerName,
            "MiniBus Azure Storage container name cannot be empty.",
            "MiniBus Azure Storage container name cannot contain whitespace.");

        if (options.PayloadRetention is { } retention && retention <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("MiniBus Azure Storage payload retention must be greater than zero.");
        }

        if (options.PayloadIdFactory is null)
        {
            throw new InvalidOperationException("MiniBus Azure Storage PayloadIdFactory cannot be null.");
        }
    }

    private static void ValidateCommon(MiniBusAzureStoragePersistenceOptions options)
    {
        if (options.UtcNowProvider is null)
        {
            throw new InvalidOperationException("MiniBus Azure Storage UtcNowProvider cannot be null.");
        }
    }

    private static void ValidateContainerName(
        string containerName,
        string emptyMessage,
        string whitespaceMessage)
    {
        if (string.IsNullOrWhiteSpace(containerName))
        {
            throw new InvalidOperationException(emptyMessage);
        }

        if (containerName.Any(char.IsWhiteSpace))
        {
            throw new InvalidOperationException(whitespaceMessage);
        }
    }

    public static void ApplyConnectionStringFactory(MiniBusAzureStoragePersistenceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            return;
        }

        var connectionString = options.ConnectionString;
        if (options.BlobContainerClientFactory is null)
        {
            options.BlobContainerClientFactory = CreateBlobContainerClientFactory(connectionString, options.ContainerName);
        }

        if (options.AuditBlobContainerClientFactory is null)
        {
            options.AuditBlobContainerClientFactory = CreateBlobContainerClientFactory(connectionString, options.AuditContainerName);
        }
    }

    private static Func<BlobContainerClient> CreateBlobContainerClientFactory(
        string connectionString,
        string containerName)
    {
        return () => new BlobContainerClient(connectionString, containerName);
    }
}
