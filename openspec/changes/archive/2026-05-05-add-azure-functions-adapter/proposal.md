## Why

MiniBus has core message contracts, serializer-based handler invocation, and Azure Service Bus transport dispatch, but Azure Functions isolated worker triggers still need a complete adapter into MiniBus processing. This change enables Service Bus trigger messages to be processed inside Azure Functions while keeping function classes thin and business handlers free of Azure Functions and Azure Service Bus SDK dependencies.

## What Changes

- Add or update the `MiniBus.AzureFunctions` package for Azure Functions isolated worker integration.
- Introduce `MiniBusProcessor` as the inbound adapter entry point for Azure Service Bus trigger messages.
- Add `ProcessAsync(ServiceBusReceivedMessage, CancellationToken)` for processing without adapter-owned settlement.
- Add `ProcessAsync(ServiceBusReceivedMessage, ServiceBusMessageActions, CancellationToken)` for processing with basic settlement.
- Convert incoming `ServiceBusReceivedMessage` instances into MiniBus core processing input by reading body, message identity, correlation metadata, message type metadata, and application properties.
- Resolve message type from MiniBus message-type headers.
- Deserialize received message bodies through the configured `MiniBus.Core` serializer.
- Extract Service Bus application properties into MiniBus headers using the existing Service Bus header mapping behavior.
- Create a `MiniBusContext` for the current incoming message.
- Invoke registered MiniBus handlers through the existing `MiniBus.Core` handler invocation pipeline.
- Dispatch outgoing `Send`, `Publish`, and `Schedule` operations from the inbound context through the existing Azure Service Bus transport.
- Complete the incoming Service Bus message on successful processing when settlement actions are provided.
- Dead-letter the incoming Service Bus message on unrecoverable processing failure when settlement actions are provided.
- Add dependency injection extensions for Azure Functions integration.
- Add unit tests for processor behavior where possible without a live Azure Functions host or Service Bus namespace.
- Add a minimal sample Azure Function wrapper using `ServiceBusTrigger`.

## Capabilities

### New Capabilities
- `azure-functions-adapter`: Azure Functions isolated worker adapter for processing Azure Service Bus trigger messages through MiniBus core handlers, dispatching outgoing operations through the Service Bus transport, and performing basic settlement.

### Modified Capabilities
- None.

## Impact

- Affected code: `src/MiniBus.AzureFunctions`, `tests/MiniBus.AzureFunctions.Tests`, `MiniBus.sln`, and a minimal sample function wrapper.
- Public APIs: introduces `MiniBusProcessor`, Azure Functions DI registration extensions, and adapter options for endpoint and transport integration; application handlers continue to depend only on `MiniBus.Core`.
- Dependencies: `MiniBus.AzureFunctions` may depend on `MiniBus.Core`, `MiniBus.AzureServiceBus`, `Azure.Messaging.ServiceBus`, and `Microsoft.Azure.Functions.Worker`.
- Systems: no SQL inbox/outbox, retries, delayed retries, sagas, source-generated wrappers, Service Bus sessions, advanced dead-letter diagnostics, or live Service Bus integration tests are included.
