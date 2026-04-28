## 1. Project setup

- [x] 1.1 Create `src/MiniBus.AzureServiceBus` as a class library targeting the solution framework and reference `MiniBus.Core`.
- [x] 1.2 Add the `Azure.Messaging.ServiceBus` package dependency to `MiniBus.AzureServiceBus`.
- [x] 1.3 Create `tests/MiniBus.AzureServiceBus.Tests`, reference the transport project, and include both new projects in `MiniBus.sln`.

## 2. Routing and sender abstractions

- [x] 2.1 Define a mockable Service Bus sender abstraction for immediate send and scheduled send operations by destination entity.
- [x] 2.2 Implement a production sender that wraps Azure Service Bus SDK senders without exposing SDK clients to handlers.
- [x] 2.3 Add transport routing/configuration for command queue destinations and event topic destinations, failing on missing or conflicting routes.
- [x] 2.4 Add explicit scheduled destination resolution for commands, events, and generic messages where supported.

## 3. Message factory and header mapping

- [x] 3.1 Implement a transport-level message factory that serializes MiniBus messages using `IMessageSerializer`.
- [x] 3.2 Map MiniBus headers to Service Bus `ApplicationProperties` using the MiniBus header keys and string values.
- [x] 3.3 Mirror core metadata such as message id, correlation id, content type, and message type into matching Service Bus system properties where available.
- [x] 3.4 Implement mapping from Service Bus application properties back to MiniBus string headers for receive-side adapters.

## 4. Dispatch operations

- [x] 4.1 Implement queue command sending that resolves command destinations and sends one Service Bus message through the sender abstraction.
- [x] 4.2 Implement topic event publishing that resolves event topic destinations and sends one Service Bus message through the sender abstraction.
- [x] 4.3 Implement scheduled message sending that resolves the destination, creates the Service Bus message, and schedules it for the requested due time.
- [x] 4.4 Ensure send, publish, and schedule fail before calling the sender abstraction when required routing is missing.

## 5. Unit tests

- [x] 5.1 Add tests for command route resolution, event topic route resolution, conflicting route registration, and missing-route failures.
- [x] 5.2 Add tests verifying command send, event publish, and scheduled dispatch call the sender abstraction with expected destinations and due times.
- [x] 5.3 Add tests verifying the message factory uses `IMessageSerializer` with the concrete message type and sets the serialized body.
- [x] 5.4 Add tests verifying MiniBus headers map to Service Bus application properties and Service Bus application properties map back to MiniBus headers.
- [x] 5.5 Add tests verifying Service Bus system properties mirror MiniBus message id, correlation id, content type, and message type metadata where supported.

## 6. Verification

- [x] 6.1 Build the solution.
- [x] 6.2 Run `MiniBus.Core.Tests` and `MiniBus.AzureServiceBus.Tests`.
- [x] 6.3 Review public APIs to confirm handler-facing code remains free of Azure Service Bus SDK dependencies.
