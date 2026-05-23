using Microsoft.Extensions.Configuration;

namespace MiniBus.Samples.Inventory.FunctionApp;

public static class InventorySampleServiceBusConnection
{
    public static string GetConnectionString(IConfiguration configuration)
    {
        var connectionString = configuration[InventoryTopology.ServiceBusConnectionSetting];
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Set '{InventoryTopology.ServiceBusConnectionSetting}' before the Inventory sample receives from Azure Service Bus.");
        }

        return connectionString;
    }
}
