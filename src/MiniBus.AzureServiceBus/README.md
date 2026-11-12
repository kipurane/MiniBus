# MiniBus.AzureServiceBus

The message factory uses the registered `MiniBusMessageIdentityRegistry` for wire identities. Pass the shared registry explicitly when constructing transport services outside dependency injection. See the root README for stable identity rollout guidance.

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

The Billing Function App sample under `samples/MiniBus.Samples.Billing.FunctionApp` includes a repo-owned Azure Service Bus emulator topology and uses the same `ServiceBusClient` plus `AzureServiceBusSender` registration locally. Its sibling Inventory Function App under `samples/MiniBus.Samples.Inventory.FunctionApp` consumes the Billing workflow's `ReserveInventory` command from `inventory-queue`, making the sample the runnable local transport reference path for a focused two-endpoint flow.

Opt-in live integration tests live under `tests/MiniBus.AzureServiceBus.IntegrationTests`. They require `MINIBUS_RUN_LIVE_AZURE_SERVICEBUS_TESTS=true` plus `MINIBUS_AZURE_SERVICEBUS_CONNECTION_STRING` for a dedicated namespace with send, listen, and entity-management rights:

```bash
dotnet test tests/MiniBus.AzureServiceBus.IntegrationTests/MiniBus.AzureServiceBus.IntegrationTests.csproj \
  --filter Category=LiveAzureServiceBus
```

The suite creates isolated `minibus-live` entities, uses bounded waits, and performs best-effort cleanup. Missing opt-in or configuration skips live tests; failures after explicit enablement remain test failures. Credentials are not printed. See the integration test README for cost, troubleshooting, and stale-resource cleanup guidance.

Azure Functions source-generated trigger wrappers live in the optional `MiniBus.AzureFunctions.SourceGenerators` package. Automatic production topology provisioning and one-topic-per-event-type topology helpers remain future work.
