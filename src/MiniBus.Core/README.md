# MiniBus.Core

`MiniBus.Core` contains the handler-facing contracts and framework abstractions used by all MiniBus packages.

It includes:

- Message contracts: `IMessage`, `ICommand`, and `IEvent`.
- Handler contracts and invocation helpers through `IHandleMessages<TMessage>`.
- Handler-facing `MiniBusContext` metadata and outgoing `Send`, `Publish`, and `Schedule` operations.
- Serialization through `IMessageSerializer` and `SystemTextJsonMessageSerializer`.
- Recoverability options and decisions.
- Saga contracts, explicit correlation mapping, in-memory saga persistence for tests and samples, and timeout message contracts.
- Claim-check, audit, inbox, outbox, and persistence abstractions used by transport and persistence packages.

Most applications reference this package through companion packages such as `MiniBus.AzureFunctions`, `MiniBus.AzureServiceBus`, `MiniBus.Persistence.Sql`, and `MiniBus.Testing`.

```csharp
public sealed record CreateInvoice(string InvoiceId, string CustomerId, decimal Amount) : ICommand;

public sealed class CreateInvoiceHandler : IHandleMessages<CreateInvoice>
{
    public Task Handle(
        CreateInvoice message,
        MiniBusContext context,
        CancellationToken cancellationToken)
    {
        return context.Publish(
            new InvoiceCreated(message.InvoiceId, message.CustomerId, message.Amount),
            cancellationToken);
    }
}
```

`MiniBus.Core` does not host processors, Azure Functions triggers, Azure Service Bus clients, SQL persistence, Azure Storage clients, or test assertion libraries. Those capabilities live in companion packages.
