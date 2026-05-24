using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;
using MiniBus.Core.Persistence;
using MiniBus.Core.Sagas;

namespace MiniBus.Persistence.Sql.DependencyInjection;

public static class MiniBusSqlPersistenceServiceCollectionExtensions
{
    private static readonly Func<IServiceProvider, ISqlMiniBusOutboxDispatchSignal> DefaultNoopDispatchSignalFactory =
        CreateDefaultNoopDispatchSignal;

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
        services.TryAddSingleton<SqlMiniBusPersistenceRegistrationMarker>();
        services.AddSingleton<ISqlMiniBusOutboxStore, SqlMiniBusOutboxStore>();
        services.AddSingleton<SqlMiniBusOutboxDispatcher>();
        services.TryAddSingleton<NoopSqlMiniBusOutboxDispatchSignal>();
        services.TryAddSingleton<ISqlMiniBusOutboxDispatchSignal>(DefaultNoopDispatchSignalFactory);
        services.AddSingleton<IMiniBusPersistenceSessionFactory>(serviceProvider =>
            new SqlMiniBusPersistenceSessionFactory(
                serviceProvider.GetRequiredService<MiniBusSqlPersistenceOptions>(),
                serviceProvider.GetRequiredService<SqlOutboxOperationSerializer>(),
                serviceProvider.GetRequiredService<ISqlMiniBusOutboxDispatchSignal>(),
                serviceProvider.GetService<ILogger<SqlMiniBusPersistenceSession>>()));
        RegisterSqlSagaPersistence(services);

        return services;
    }

    public static IServiceCollection AddMiniBusSqlHostedOutboxDispatch(
        this IServiceCollection services,
        Action<MiniBusSqlHostedOutboxDispatchOptions>? configureOptions = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        EnsureSqlPersistenceRegistered(services);

        var options = new MiniBusSqlHostedOutboxDispatchOptions();
        configureOptions?.Invoke(options);
        var settings = options.ToSettings();

        services.RemoveAll<MiniBusSqlHostedOutboxDispatchSettings>();
        services.AddSingleton(settings);
        ReplaceDefaultDispatchSignal(services);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, SqlMiniBusOutboxHostedDispatcher>());

        return services;
    }

    private static void ReplaceDefaultDispatchSignal(IServiceCollection services)
    {
        var removedDefaultSignal = false;

        for (var index = services.Count - 1; index >= 0; index--)
        {
            var descriptor = services[index];
            if (descriptor.ServiceType == typeof(ISqlMiniBusOutboxDispatchSignal)
                && IsDefaultNoopDispatchSignal(descriptor))
            {
                services.RemoveAt(index);
                removedDefaultSignal = true;
            }
        }

        if (services.Any(descriptor => descriptor.ServiceType == typeof(ISqlMiniBusOutboxDispatchSignal)))
        {
            return;
        }

        if (removedDefaultSignal)
        {
            services.RemoveAll<NoopSqlMiniBusOutboxDispatchSignal>();
        }

        services.AddSingleton<ISqlMiniBusOutboxDispatchSignal, SqlMiniBusOutboxDispatchSignal>();
    }

    private static bool IsDefaultNoopDispatchSignal(ServiceDescriptor descriptor)
    {
        return ReferenceEquals(descriptor.ImplementationFactory, DefaultNoopDispatchSignalFactory);
    }

    private static ISqlMiniBusOutboxDispatchSignal CreateDefaultNoopDispatchSignal(IServiceProvider serviceProvider)
    {
        return serviceProvider.GetRequiredService<NoopSqlMiniBusOutboxDispatchSignal>();
    }

    private static void EnsureSqlPersistenceRegistered(IServiceCollection services)
    {
        if (services.Any(descriptor => descriptor.ServiceType == typeof(SqlMiniBusPersistenceRegistrationMarker)))
        {
            return;
        }

        throw new InvalidOperationException(
            "MiniBus SQL hosted outbox dispatch requires SQL persistence services. " +
            $"Call {nameof(AddMiniBusSqlPersistence)} before {nameof(AddMiniBusSqlHostedOutboxDispatch)}.");
    }

    private sealed class SqlMiniBusPersistenceRegistrationMarker;

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
