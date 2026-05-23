using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using MiniBus.AzureFunctions.Processing;

namespace MiniBus.Samples.Inventory.FunctionApp;

public sealed class InventoryInputFunction
{
    private readonly MiniBusProcessor _processor;

    public InventoryInputFunction(MiniBusProcessor processor)
    {
        _processor = processor;
    }

    [Function("InventoryInput")]
    public Task Run(
        [ServiceBusTrigger(InventoryTopology.InputQueue, Connection = InventoryTopology.ServiceBusConnectionSetting)]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions actions,
        CancellationToken cancellationToken)
    {
        return _processor.ProcessAsync(message, actions, cancellationToken);
    }
}
