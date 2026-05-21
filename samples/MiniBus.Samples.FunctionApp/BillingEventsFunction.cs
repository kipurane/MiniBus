using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using MiniBus.AzureFunctions.Processing;

namespace MiniBus.Samples.FunctionApp;

public sealed class BillingEventsFunction
{
    private readonly MiniBusProcessor _processor;

    public BillingEventsFunction(MiniBusProcessor processor)
    {
        _processor = processor;
    }

    [Function("BillingEvents")]
    public Task Run(
        [ServiceBusTrigger("domain-events", "billing", Connection = "ServiceBus")]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions actions,
        CancellationToken cancellationToken)
    {
        return _processor.ProcessAsync(message, actions, cancellationToken);
    }
}
