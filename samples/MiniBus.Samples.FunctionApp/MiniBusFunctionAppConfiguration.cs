using Microsoft.Extensions.DependencyInjection;
using MiniBus.AzureFunctions.DependencyInjection;
using MiniBus.Core.Sagas;
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

        var sagaRegistry = new SagaRegistry();
        sagaRegistry.Register<BillingSaga, BillingSagaData>();
        services.AddSingleton(sagaRegistry);
        services.AddSingleton<ISagaPersistence, InMemorySagaPersistence>();
        services.AddSingleton<SagaInvoker>();

        return services;
    }
}
