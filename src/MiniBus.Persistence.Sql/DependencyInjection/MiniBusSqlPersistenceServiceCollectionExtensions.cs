using Microsoft.Extensions.DependencyInjection;
using MiniBus.Core.Persistence;

namespace MiniBus.Persistence.Sql.DependencyInjection;

public static class MiniBusSqlPersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddMiniBusSqlPersistence(
        this IServiceCollection services,
        Action<MiniBusSqlPersistenceOptions> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureOptions);

        var options = new MiniBusSqlPersistenceOptions();
        configureOptions(options);

        services.AddSingleton(options);
        services.AddSingleton<SqlOutboxOperationSerializer>();
        services.AddSingleton<IMiniBusPersistenceSessionFactory, SqlMiniBusPersistenceSessionFactory>();
        services.AddSingleton<ISqlMiniBusOutboxStore, SqlMiniBusOutboxStore>();
        services.AddSingleton<SqlMiniBusOutboxDispatcher>();

        return services;
    }
}
