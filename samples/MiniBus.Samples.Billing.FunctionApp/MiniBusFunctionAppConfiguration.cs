using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MiniBus.AzureFunctions.DependencyInjection;
using MiniBus.AzureServiceBus.Dispatching;
using MiniBus.AzureServiceBus.Recoverability;
using MiniBus.AzureServiceBus.Routing;
using MiniBus.AzureServiceBus.TransportMessageMapping;
using MiniBus.Core.Handlers;
using MiniBus.Core.Persistence;
using MiniBus.Core.Sagas;
using MiniBus.Core.Serialization;
using MiniBus.Persistence.Sql.DependencyInjection;
using MiniBus.Samples.Contracts.Billing;
using MiniBus.Samples.Contracts.Inventory;
using MiniBus.Samples.Billing.FunctionApp.Handlers;
using MiniBus.Samples.Billing.FunctionApp.Sagas;

namespace MiniBus.Samples.Billing.FunctionApp;

public static class MiniBusFunctionAppConfiguration
{
    public static IServiceCollection AddBillingMiniBus(
        this IServiceCollection services,
        IConfiguration configuration)
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
        services.AddSingleton(_ => new ServiceBusClient(
            BillingSampleServiceBusConnection.GetConnectionString(configuration)));
        services.AddSingleton<IAzureServiceBusSender, BillingSampleServiceBusSender>();
        services.AddSingleton<IAzureServiceBusDelayedRetryScheduler, AzureServiceBusDelayedRetryScheduler>();

        var sagaRegistry = new SagaRegistry();
        sagaRegistry.Register<BillingSaga, BillingSagaData>();
        services.AddSingleton(sagaRegistry);
        services.AddSingleton<SagaInvoker>();

        if (BillingSampleSqlPersistence.IsEnabled(configuration))
        {
            services.AddSingleton<IMiniBusOutboxDispatcher>(
                serviceProvider => serviceProvider.GetRequiredService<AzureServiceBusTransportDispatcher>());
            services.AddMiniBusSqlPersistence(
                BillingSampleSqlPersistence.GetConnectionString(configuration),
                options =>
                {
                    options.SchemaName = BillingSampleSqlPersistence.GetSchemaName(configuration);
                });
        }
        else
        {
            services.AddSingleton<ISagaPersistence, InMemorySagaPersistence>();
        }

        return services;
    }

    private static AzureServiceBusTransportRoutes CreateRoutes()
    {
        var routes = new AzureServiceBusTransportRoutes();

        routes.MapCommand<CreateInvoice>(BillingTopology.InputQueue);
        routes.MapCommand<SendInvoiceReceipt>(BillingTopology.ReceiptsQueue);
        routes.MapCommand<ReserveInventory>(BillingTopology.InventoryQueue);
        routes.MapEvent<InvoiceCreated>(BillingTopology.EventsTopic);
        routes.MapScheduledMessage<InvoicePaymentTimeout>(BillingTopology.TimeoutsQueue);

        return routes;
    }
}
