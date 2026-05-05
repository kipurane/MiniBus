# MiniBus.AzureFunctions

Manual Azure Functions isolated worker wrappers keep trigger declarations in the function app and delegate processing to `MiniBusProcessor`.

```csharp
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using MiniBus.AzureFunctions.Processing;

public sealed class BillingInputFunction
{
    private readonly MiniBusProcessor _processor;

    public BillingInputFunction(MiniBusProcessor processor)
    {
        _processor = processor;
    }

    [Function("BillingInput")]
    public Task Run(
        [ServiceBusTrigger("billing-queue", Connection = "ServiceBus")]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions actions,
        CancellationToken cancellationToken)
    {
        return _processor.ProcessAsync(message, actions, cancellationToken);
    }
}
```

Handlers still implement `IHandleMessages<TMessage>` from `MiniBus.Core` and receive only the deserialized message, `MiniBusContext`, and `CancellationToken`.
