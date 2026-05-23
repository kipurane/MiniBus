# MiniBus.AzureServiceBus

`MiniBus.AzureServiceBus` provides Azure Service Bus transport support for MiniBus.

It includes:

- `AzureServiceBusTransportRoutes` for explicit command, event, and scheduled-message destinations.
- `AzureServiceBusMessageFactory` for MiniBus envelope and header mapping.
- `AzureServiceBusTransportDispatcher` for MiniBus outgoing `Send`, `Publish`, and `Schedule` operations.
- `AzureServiceBusDelayedRetryScheduler` for Azure Service Bus scheduled-message retry copies.

This package depends on `MiniBus.Core` and `Azure.Messaging.ServiceBus`. It is usually used with `MiniBus.AzureFunctions` for Azure Functions isolated worker processing.

```csharp
var routes = new AzureServiceBusTransportRoutes();
routes.MapCommand<CreateInvoice>("billing-queue");
routes.MapCommand<SendInvoiceReceipt>("billing-receipts");
routes.MapEvent<InvoiceCreated>("domain-events");
routes.MapScheduledMessage<InvoicePaymentTimeout>("billing-timeouts");

services.AddSingleton(routes);
services.AddSingleton<AzureServiceBusMessageFactory>();
services.AddSingleton<AzureServiceBusTransportDispatcher>();
services.AddSingleton(_ => new ServiceBusClient(serviceBusConnectionString));
services.AddSingleton<IAzureServiceBusSender, AzureServiceBusSender>();
services.AddSingleton<IAzureServiceBusDelayedRetryScheduler, AzureServiceBusDelayedRetryScheduler>();
```

Routes are explicit. A missing route fails dispatch instead of guessing a destination. Applications remain responsible for creating Azure Service Bus queues, topics, subscriptions, and any duplicate-detection settings they need.

The Billing Function App sample under `samples/MiniBus.Samples.FunctionApp` includes a repo-owned Azure Service Bus emulator topology and uses the same `ServiceBusClient` plus `AzureServiceBusSender` registration locally. Its sibling Inventory Function App under `samples/MiniBus.Samples.Inventory.FunctionApp` consumes the Billing workflow's `ReserveInventory` command from `inventory-queue`, making the sample the runnable local transport reference path for a focused two-endpoint flow. Live Azure Service Bus coverage remains separate.

Azure Functions source-generated trigger wrappers live in the optional `MiniBus.AzureFunctions.SourceGenerators` package. Topology provisioning, live Azure integration tests, and one-topic-per-event-type topology helpers are future work.
