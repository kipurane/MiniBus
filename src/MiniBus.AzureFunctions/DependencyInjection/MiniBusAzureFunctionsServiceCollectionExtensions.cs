using Microsoft.Extensions.DependencyInjection;
using MiniBus.AzureFunctions.Processing;
using MiniBus.AzureServiceBus.Recoverability;
using MiniBus.Core.Handlers;
using MiniBus.Core.Recoverability;

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
        services.AddSingleton<RecoverabilityDecisionMaker>();
        services.AddSingleton<IAzureServiceBusDelayedRetryScheduler, AzureServiceBusDelayedRetryScheduler>();
        services.AddSingleton<MiniBusProcessor>();

        return services;
    }
}
