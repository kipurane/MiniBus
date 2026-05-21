using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MiniBus.AzureFunctions.DependencyInjection;
using MiniBus.AzureServiceBus.Dispatching;
using MiniBus.AzureServiceBus.Recoverability;
using MiniBus.AzureServiceBus.Routing;
using MiniBus.AzureServiceBus.TransportMessageMapping;
using MiniBus.Core.Handlers;
using MiniBus.Core.Serialization;
using MiniBus.FunctionApp.Template.Contracts;
using MiniBus.FunctionApp.Template.Handlers;

namespace MiniBus.FunctionApp.Template;

public static class MiniBusConfiguration
{
    public static IServiceCollection AddStarterMiniBus(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddMiniBusAzureFunctions(options =>
        {
            options.EndpointName = "Starter";
            options.Recoverability.ImmediateRetries = 3;
            options.Recoverability.DelayedRetries.Add(TimeSpan.FromSeconds(10));
            options.Recoverability.DelayedRetries.Add(TimeSpan.FromMinutes(1));
            options.Recoverability.DeadLetterAfterRetriesExhausted = true;
        });

        services.AddSingleton<IMessageSerializer, SystemTextJsonMessageSerializer>();
        services.AddTransient<IHandleMessages<SubmitOrder>, SubmitOrderHandler>();

        services.AddSingleton(CreateRoutes());
        services.AddSingleton<AzureServiceBusMessageFactory>();
        services.AddSingleton<AzureServiceBusTransportDispatcher>();
        services.AddSingleton(_ => new ServiceBusClient(GetServiceBusConnectionString(configuration)));
        services.AddSingleton<IAzureServiceBusSender, AzureServiceBusSender>();
        services.AddSingleton<IAzureServiceBusDelayedRetryScheduler, AzureServiceBusDelayedRetryScheduler>();

        return services;
    }

    private static AzureServiceBusTransportRoutes CreateRoutes()
    {
        var routes = new AzureServiceBusTransportRoutes();

        routes.MapCommand<SubmitOrder>(StarterTopology.InputQueue);
        routes.MapEvent<OrderSubmitted>(StarterTopology.EventsTopic);

        return routes;
    }

    private static string GetServiceBusConnectionString(IConfiguration configuration)
    {
        var connectionString = configuration[StarterTopology.ServiceBusConnectionSetting];
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Set '{StarterTopology.ServiceBusConnectionSetting}' before the starter sends through Azure Service Bus.");
        }

        return connectionString;
    }
}
