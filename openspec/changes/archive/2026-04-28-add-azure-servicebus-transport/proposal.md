## Why

MiniBus now has transport-agnostic message contracts, routing, headers, context, and serialization, but it still has no Azure Service Bus dispatch implementation. Adding the Service Bus transport next turns outgoing `Send`, `Publish`, and `Schedule` work into concrete Azure-native messages while preserving the thin-adapter architecture.

## What Changes

- Add a `MiniBus.AzureServiceBus` package that depends on `MiniBus.Core` and the Azure Service Bus SDK.
- Introduce a Service Bus sender abstraction used by MiniBus transport dispatch code instead of exposing Azure SDK clients to handlers.
- Implement queue command sending using MiniBus command routing.
- Implement topic event publishing using explicit event-to-topic transport routing.
- Implement scheduled message sending for commands, events, and generic messages where a destination can be resolved.
- Map MiniBus headers to Service Bus application properties when creating outgoing messages.
- Map Service Bus application properties back to MiniBus headers for later receive adapters.
- Serialize message bodies through the `MiniBus.Core` serializer abstraction.
- Add a transport-level message factory responsible for creating Service Bus messages from MiniBus messages, headers, and serialization metadata.
- Add unit tests with mocked sender abstractions and SDK message inspection where possible.

## Capabilities

### New Capabilities
- `azure-servicebus-transport`: Azure Service Bus transport support for sending commands to queues, publishing events to topics, scheduling outgoing messages, and mapping MiniBus messages and headers to Service Bus messages.

### Modified Capabilities
- None.

## Impact

- Affected code: new `src/MiniBus.AzureServiceBus`, new `tests/MiniBus.AzureServiceBus.Tests`, `MiniBus.sln`, and any shared test or project configuration needed to include the package.
- Public APIs: introduces transport-facing abstractions and configuration for Azure Service Bus send, publish, schedule, header mapping, and message creation; application handlers continue to depend only on `MiniBus.Core`.
- Dependencies: adds `Azure.Messaging.ServiceBus` to the Azure Service Bus package and test dependencies for unit testing.
- Systems: no Azure Functions trigger adapter, SQL inbox/outbox, retry, dead-lettering, saga, source generation, or real Service Bus end-to-end integration tests are included.
