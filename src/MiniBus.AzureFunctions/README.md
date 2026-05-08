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

Recoverability is configured with the Azure Functions adapter registration:

```csharp
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
```

Immediate retries run inside the same `MiniBusProcessor` invocation. Delayed retries use Azure Service Bus scheduled message copies and preserve MiniBus correlation, original message id, retry, and exception headers.

Minimal saga support uses core saga contracts and explicit correlation mappings:

```csharp
public sealed class BillingSagaData : ISagaData
{
    public Guid Id { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public bool InvoiceCreated { get; set; }
}

public sealed class BillingSaga :
    MiniBusSaga<BillingSagaData>,
    IHandleSagaMessages<CreateInvoice>
{
    public override void ConfigureHowToFindSaga(SagaMapper<BillingSagaData> mapper)
    {
        mapper.StartsWith<CreateInvoice>(message => message.InvoiceId);
    }

    public Task Handle(CreateInvoice message, MiniBusContext context, CancellationToken cancellationToken)
    {
        Data.InvoiceCreated = true;
        MarkAsComplete();
        return Task.CompletedTask;
    }
}
```

Register saga mappings explicitly during startup:

```csharp
var sagaRegistry = new SagaRegistry();
sagaRegistry.Register<BillingSaga, BillingSagaData>();

services.AddSingleton(sagaRegistry);
services.AddSingleton<ISagaPersistence, InMemorySagaPersistence>();
services.AddSingleton<SagaInvoker>();
```

`AddMiniBusAzureFunctions` does not register `SagaRegistry` or `SagaInvoker` by default; saga processing is opt-in through `MiniBusProcessorOptions.EnableSagas`. It registers an `UnconfiguredSagaPersistence` placeholder so production apps must choose a real saga store explicitly. `InMemorySagaPersistence` is intended for tests and samples. Production SQL saga persistence is deferred until the SQL persistence package exists.
