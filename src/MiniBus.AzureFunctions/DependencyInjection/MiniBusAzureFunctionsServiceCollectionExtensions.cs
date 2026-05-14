using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MiniBus.AzureFunctions.Processing;
using MiniBus.AzureServiceBus.Recoverability;
using MiniBus.Core.Handlers;
using MiniBus.Core.Recoverability;
using MiniBus.Core.Sagas;

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
        services.TryAddSingleton<ISagaPersistence, UnconfiguredSagaPersistence>();
        services.AddSingleton<IAzureServiceBusDelayedRetryScheduler, AzureServiceBusDelayedRetryScheduler>();
        services.AddSingleton(serviceProvider => new MiniBusProcessor(
            serviceProvider.GetRequiredService<MiniBus.Core.Serialization.IMessageSerializer>(),
            serviceProvider.GetRequiredService<MessageHandlerInvoker>(),
            serviceProvider,
            serviceProvider.GetRequiredService<MiniBusProcessorOptions>(),
            serviceProvider.GetRequiredService<RecoverabilityDecisionMaker>(),
            serviceProvider.GetService<SagaInvoker>()));

        return services;
    }
}
