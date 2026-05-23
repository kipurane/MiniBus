using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MiniBus.AzureFunctions.DependencyInjection;
using MiniBus.AzureServiceBus.Recoverability;
using MiniBus.Core.Handlers;
using MiniBus.Core.Serialization;
using MiniBus.Samples.Contracts.Inventory;
using MiniBus.Samples.Inventory.FunctionApp.Handlers;

namespace MiniBus.Samples.Inventory.FunctionApp;

public static class MiniBusInventoryFunctionAppConfiguration
{
    public static IServiceCollection AddInventoryMiniBus(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddMiniBusAzureFunctions(options =>
        {
            options.EndpointName = "Inventory";
            options.Recoverability.ImmediateRetries = 3;
            options.Recoverability.DelayedRetries.Clear();
            options.Recoverability.DeadLetterAfterRetriesExhausted = true;
        });

        // Inventory does not dispatch outgoing work or schedule delayed retries in this first endpoint-boundary slice.
        services.RemoveAll<IAzureServiceBusDelayedRetryScheduler>();

        services.AddSingleton<IMessageSerializer, SystemTextJsonMessageSerializer>();
        services.AddSingleton<InventoryReservationLog>();
        services.AddTransient<IHandleMessages<ReserveInventory>, ReserveInventoryHandler>();

        _ = InventorySampleServiceBusConnection.GetConnectionString(configuration);

        return services;
    }
}
