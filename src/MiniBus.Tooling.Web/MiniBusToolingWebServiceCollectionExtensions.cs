using Microsoft.Extensions.DependencyInjection.Extensions;
using MiniBus.Tooling.Core;
using MiniBus.Tooling.Sql;

namespace MiniBus.Tooling.Web;

public static class MiniBusToolingWebServiceCollectionExtensions
{
    public static IServiceCollection AddMiniBusToolingWeb(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var options = new MiniBusToolingWebOptions();
        configuration.GetSection("MiniBus:Tooling:Sql").Bind(options);

        if (!string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            services.TryAddSingleton(_ =>
                new SqlMiniBusToolingReader(new MiniBusSqlToolingOptions
                {
                    ConnectionString = options.ConnectionString,
                    IsUiAvailable = true,
                    SchemaName = string.IsNullOrWhiteSpace(options.SchemaName)
                        ? "MiniBus"
                        : options.SchemaName
                }));
            services.TryAddSingleton<IMiniBusInboxToolingReader>(
                provider => provider.GetRequiredService<SqlMiniBusToolingReader>());
            services.TryAddSingleton<IMiniBusOutboxToolingReader>(
                provider => provider.GetRequiredService<SqlMiniBusToolingReader>());
            services.TryAddSingleton<IMiniBusSagaToolingReader>(
                provider => provider.GetRequiredService<SqlMiniBusToolingReader>());
            services.TryAddSingleton<IMiniBusTimelineToolingReader>(
                provider => provider.GetRequiredService<SqlMiniBusToolingReader>());
            return services;
        }

        services.TryAddSingleton<UnsupportedMiniBusToolingReader>();
        services.TryAddSingleton<IMiniBusInboxToolingReader>(
            provider => provider.GetRequiredService<UnsupportedMiniBusToolingReader>());
        services.TryAddSingleton<IMiniBusOutboxToolingReader>(
            provider => provider.GetRequiredService<UnsupportedMiniBusToolingReader>());
        services.TryAddSingleton<IMiniBusSagaToolingReader>(
            provider => provider.GetRequiredService<UnsupportedMiniBusToolingReader>());
        services.TryAddSingleton<IMiniBusTimelineToolingReader>(
            provider => provider.GetRequiredService<UnsupportedMiniBusToolingReader>());

        return services;
    }
}
