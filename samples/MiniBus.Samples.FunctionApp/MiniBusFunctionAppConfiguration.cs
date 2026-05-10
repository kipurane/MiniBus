using Microsoft.Extensions.DependencyInjection;
using MiniBus.AzureFunctions.DependencyInjection;
using MiniBus.AzureServiceBus.Dispatching;
using MiniBus.AzureServiceBus.Recoverability;
using MiniBus.AzureServiceBus.Routing;
using MiniBus.AzureServiceBus.TransportMessageMapping;
using MiniBus.Core.Handlers;
using MiniBus.Core.Sagas;
using MiniBus.Core.Serialization;
using MiniBus.Samples.FunctionApp.Contracts;
using MiniBus.Samples.FunctionApp.Handlers;
using MiniBus.Samples.FunctionApp.Sagas;

namespace MiniBus.Samples.FunctionApp;

public static class MiniBusFunctionAppConfiguration
{
    public static IServiceCollection AddBillingMiniBus(this IServiceCollection services)
    {
        services.AddMiniBusAzureFunctions(options =>
        {
            options.EndpointName = "Billing";
            options.EnableSagas = true;
            options.Recoverability.ImmediateRetries = 3;
            options.Recoverability.DelayedRetries.Add(TimeSpan.FromSeconds(10));
            options.Recoverability.DelayedRetries.Add(TimeSpan.FromMinutes(1));
            options.Recoverability.DelayedRetries.Add(TimeSpan.FromMinutes(5));
            options.Recoverability.DeadLetterAfterRetriesExhausted = true;
        });

        services.AddSingleton<IMessageSerializer, SystemTextJsonMessageSerializer>();
        services.AddTransient<IHandleMessages<CreateInvoice>, CreateInvoiceHandler>();

        services.AddSingleton(CreateRoutes());
        services.AddSingleton<AzureServiceBusMessageFactory>();
        services.AddSingleton<AzureServiceBusTransportDispatcher>();
        services.AddSingleton<IAzureServiceBusSender, SampleServiceBusSender>();
        services.AddSingleton<IAzureServiceBusDelayedRetryScheduler, AzureServiceBusDelayedRetryScheduler>();

        var sagaRegistry = new SagaRegistry();
        sagaRegistry.Register<BillingSaga, BillingSagaData>();
        services.AddSingleton(sagaRegistry);
        services.AddSingleton<ISagaPersistence, InMemorySagaPersistence>();
        services.AddSingleton<SagaInvoker>();

        return services;
    }

    private static AzureServiceBusTransportRoutes CreateRoutes()
    {
        var routes = new AzureServiceBusTransportRoutes();

        routes.MapCommand<CreateInvoice>("billing-queue");
        routes.MapCommand<SendInvoiceReceipt>("billing-receipts");
        routes.MapEvent<InvoiceCreated>("domain-events");

        return routes;
    }
}
