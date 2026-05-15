using Azure.Storage.Blobs;

namespace MiniBus.Persistence.AzureStorage;

public static class MiniBusAzureStoragePersistenceOptionsValidator
{
    public static void Validate(MiniBusAzureStoragePersistenceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.BlobContainerClientFactory is null)
        {
            throw new InvalidOperationException(
                "MiniBus Azure Storage persistence requires a Blob Storage connection string or BlobContainerClient factory.");
        }

        if (string.IsNullOrWhiteSpace(options.ContainerName))
        {
            throw new InvalidOperationException("MiniBus Azure Storage container name cannot be empty.");
        }

        if (options.ContainerName.Any(char.IsWhiteSpace))
        {
            throw new InvalidOperationException("MiniBus Azure Storage container name cannot contain whitespace.");
        }

        if (options.PayloadRetention is { } retention && retention <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("MiniBus Azure Storage payload retention must be greater than zero.");
        }

        if (options.UtcNowProvider is null)
        {
            throw new InvalidOperationException("MiniBus Azure Storage UtcNowProvider cannot be null.");
        }

        if (options.PayloadIdFactory is null)
        {
            throw new InvalidOperationException("MiniBus Azure Storage PayloadIdFactory cannot be null.");
        }
    }

    public static void ApplyConnectionStringFactory(MiniBusAzureStoragePersistenceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.BlobContainerClientFactory is not null
            || string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            return;
        }

        var connectionString = options.ConnectionString;
        var containerName = options.ContainerName;
        options.BlobContainerClientFactory = () => new BlobContainerClient(connectionString, containerName);
    }
}
