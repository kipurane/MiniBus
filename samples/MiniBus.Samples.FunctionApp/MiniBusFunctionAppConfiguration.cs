using Microsoft.Extensions.DependencyInjection;
using MiniBus.AzureFunctions.DependencyInjection;

namespace MiniBus.Samples.FunctionApp;

public static class MiniBusFunctionAppConfiguration
{
    public static IServiceCollection AddBillingMiniBus(this IServiceCollection services)
    {
        return services.AddMiniBusAzureFunctions(options =>
        {
            options.EndpointName = "Billing";
            options.Recoverability.ImmediateRetries = 3;
            options.Recoverability.DelayedRetries.Add(TimeSpan.FromSeconds(10));
            options.Recoverability.DelayedRetries.Add(TimeSpan.FromMinutes(1));
            options.Recoverability.DelayedRetries.Add(TimeSpan.FromMinutes(5));
            options.Recoverability.DeadLetterAfterRetriesExhausted = true;
        });
    }
}
