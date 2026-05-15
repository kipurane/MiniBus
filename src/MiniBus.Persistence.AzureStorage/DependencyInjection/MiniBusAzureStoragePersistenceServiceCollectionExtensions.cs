using Microsoft.Extensions.DependencyInjection;

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
        services.AddSingleton<IMiniBusPayloadStore, BlobMiniBusPayloadStore>();

        return services;
    }
}
