using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Data.SqlClient;
using MiniBus.Core.Persistence;
using MiniBus.Core.Sagas;

namespace MiniBus.Persistence.Sql.DependencyInjection;

public static class MiniBusSqlPersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddMiniBusSqlPersistence(
        this IServiceCollection services,
        string connectionString,
        Action<MiniBusSqlPersistenceOptions>? configureOptions = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("SQL Server connection string cannot be empty.", nameof(connectionString));
        }

        return services.AddMiniBusSqlPersistence(options =>
        {
            options.ConnectionString = connectionString;
            configureOptions?.Invoke(options);
        });
    }

    public static IServiceCollection AddMiniBusSqlPersistence(
        this IServiceCollection services,
        Action<MiniBusSqlPersistenceOptions> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureOptions);

        var options = new MiniBusSqlPersistenceOptions();
        configureOptions(options);
        ApplyConnectionStringFactory(options);

        services.AddSingleton(options);
        services.AddSingleton<SqlOutboxOperationSerializer>();
        services.AddSingleton<SqlSagaDataSerializer>();
        services.AddSingleton<SqlMiniBusOutboxMetrics>();
        services.AddSingleton<IMiniBusPersistenceSessionFactory, SqlMiniBusPersistenceSessionFactory>();
        services.AddSingleton<ISqlMiniBusOutboxStore, SqlMiniBusOutboxStore>();
        services.AddSingleton<SqlMiniBusOutboxDispatcher>();
        RegisterSqlSagaPersistence(services);

        return services;
    }

    private static void RegisterSqlSagaPersistence(IServiceCollection services)
    {
        for (var index = services.Count - 1; index >= 0; index--)
        {
            var descriptor = services[index];
            if (descriptor.ServiceType == typeof(ISagaPersistence)
                && descriptor.ImplementationType == typeof(UnconfiguredSagaPersistence))
            {
                services.RemoveAt(index);
            }
        }

        services.TryAddSingleton<ISagaPersistence, SqlSagaPersistence>();
    }

    private static void ApplyConnectionStringFactory(MiniBusSqlPersistenceOptions options)
    {
        if (options.ConnectionFactory is not null
            || string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            return;
        }

        var connectionString = options.ConnectionString;
        options.ConnectionFactory = () => new SqlConnection(connectionString);
    }
}
