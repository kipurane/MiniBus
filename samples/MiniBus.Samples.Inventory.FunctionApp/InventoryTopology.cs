namespace MiniBus.Samples.Inventory.FunctionApp;

public static class InventoryTopology
{
    public const string ServiceBusConnectionSetting = "ServiceBus";
    public const string EmulatorConnectionString =
        "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;" +
        "SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;";

    public const string InputQueue = "inventory-queue";
}
