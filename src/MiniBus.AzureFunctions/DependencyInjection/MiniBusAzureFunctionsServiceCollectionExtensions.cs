using Microsoft.Extensions.DependencyInjection;
using MiniBus.AzureFunctions.Processing;
using MiniBus.Core.Handlers;

namespace MiniBus.AzureFunctions.DependencyInjection;

public static class MiniBusAzureFunctionsServiceCollectionExtensions
{
    public static IServiceCollection AddMiniBusAzureFunctions(
        this IServiceCollection services,
        Action<MiniBusProcessorOptions>? configureOptions = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new MiniBusProcessorOptions();
        configureOptions?.Invoke(options);

        services.AddSingleton(options);
        services.AddSingleton<MessageHandlerInvoker>();
        services.AddSingleton<MiniBusProcessor>();

        return services;
    }
}
