using Microsoft.Extensions.DependencyInjection;
using MiniBus.Core.Auditing;
using MiniBus.Core.ClaimCheck;

namespace MiniBus.Persistence.AzureStorage.DependencyInjection;

public static class MiniBusAzureStoragePersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddMiniBusAzureStoragePersistence(
        this IServiceCollection services,
        string connectionString,
        string containerName,
        Action<MiniBusAzureStoragePersistenceOptions>? configureOptions = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Azure Storage connection string cannot be empty.", nameof(connectionString));
        }

        if (string.IsNullOrWhiteSpace(containerName))
        {
            throw new ArgumentException("Azure Storage payload container name cannot be empty.", nameof(containerName));
        }

        return services.AddMiniBusAzureStoragePersistence(options =>
        {
            options.ConnectionString = connectionString;
            options.ContainerName = containerName;
            configureOptions?.Invoke(options);
        });
    }

    public static IServiceCollection AddMiniBusAzureStoragePersistence(
        this IServiceCollection services,
        Action<MiniBusAzureStoragePersistenceOptions> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureOptions);

        var options = new MiniBusAzureStoragePersistenceOptions();
        configureOptions(options);
        MiniBusAzureStoragePersistenceOptionsValidator.ApplyConnectionStringFactory(options);
        MiniBusAzureStoragePersistenceOptionsValidator.Validate(options);

        services.AddSingleton(options);
        services.AddSingleton<BlobMiniBusPayloadStore>();
        services.AddSingleton<IMiniBusPayloadStore>(serviceProvider =>
            serviceProvider.GetRequiredService<BlobMiniBusPayloadStore>());
        services.AddSingleton<IMiniBusClaimCheckPayloadStore>(serviceProvider =>
            serviceProvider.GetRequiredService<BlobMiniBusPayloadStore>());

        return services;
    }

    public static IServiceCollection AddMiniBusAzureBlobClaimCheck(
        this IServiceCollection services,
        long payloadThresholdBytes)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new MiniBusClaimCheckOptions
        {
            Enabled = true,
            PayloadThresholdBytes = payloadThresholdBytes,
            Provider = MiniBusClaimCheckProviderNames.AzureBlobStorage
        };
        options.Validate();

        services.AddSingleton(options);
        return services;
    }

    public static IServiceCollection AddMiniBusAzureBlobAudit(
        this IServiceCollection services,
        string connectionString,
        string auditContainerName,
        Action<MiniBusAzureStoragePersistenceOptions>? configureOptions = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Azure Storage connection string cannot be empty.", nameof(connectionString));
        }

        if (string.IsNullOrWhiteSpace(auditContainerName))
        {
            throw new ArgumentException("Azure Storage audit container name cannot be empty.", nameof(auditContainerName));
        }

        return services.AddMiniBusAzureBlobAudit(options =>
        {
            options.ConnectionString = connectionString;
            options.AuditContainerName = auditContainerName;
            configureOptions?.Invoke(options);
        });
    }

    public static IServiceCollection AddMiniBusAzureBlobAudit(
        this IServiceCollection services,
        Action<MiniBusAzureStoragePersistenceOptions> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureOptions);

        var options = new MiniBusAzureStoragePersistenceOptions();
        configureOptions(options);
        MiniBusAzureStoragePersistenceOptionsValidator.ApplyConnectionStringFactory(options);
        MiniBusAzureStoragePersistenceOptionsValidator.ValidateAudit(options);

        services.AddSingleton<IMiniBusAuditWriter>(_ => new BlobMiniBusAuditWriter(options));

        return services;
    }
}
