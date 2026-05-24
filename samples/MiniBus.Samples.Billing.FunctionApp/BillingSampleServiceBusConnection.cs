using Microsoft.Extensions.Configuration;

namespace MiniBus.Samples.Billing.FunctionApp;

public static class BillingSampleServiceBusConnection
{
    public static string GetConnectionString(IConfiguration configuration)
    {
        var connectionString = configuration[BillingTopology.ServiceBusConnectionSetting];
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Set '{BillingTopology.ServiceBusConnectionSetting}' before the Billing sample sends through Azure Service Bus.");
        }

        return connectionString;
    }

    public static string GetSeedConnectionString()
    {
        return Environment.GetEnvironmentVariable(BillingTopology.ServiceBusConnectionSetting)
               ?? BillingTopology.EmulatorConnectionString;
    }
}
