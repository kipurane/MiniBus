namespace MiniBus.Samples.Billing.FunctionApp;

public static class BillingTopology
{
    public const string ServiceBusConnectionSetting = "ServiceBus";
    public const string EmulatorConnectionString =
        "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;" +
        "SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;";

    public const string InputQueue = "billing-queue";
    public const string ReceiptsQueue = "billing-receipts";
    public const string TimeoutsQueue = "billing-timeouts";
    public const string InventoryQueue = "inventory-queue";
    public const string EventsTopic = "domain-events";
    public const string BillingSubscription = "billing";
}
