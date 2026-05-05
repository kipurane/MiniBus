## 1. Project setup

- [x] 1.1 Create or update `src/MiniBus.AzureFunctions` as a class library targeting the solution framework and referencing `MiniBus.Core`.
- [x] 1.2 Reference `MiniBus.AzureServiceBus` from the adapter project for Service Bus header mapping and outgoing dispatch.
- [x] 1.3 Add Azure Functions isolated worker and Service Bus extension dependencies required by the adapter public API.
- [x] 1.4 Create or update `tests/MiniBus.AzureFunctions.Tests`, reference the adapter project, and include both projects in `MiniBus.sln`.

## 2. Processor input and metadata adaptation

- [x] 2.1 Define `MiniBusProcessor.ProcessAsync(ServiceBusReceivedMessage, CancellationToken)` for processing without adapter-owned settlement.
- [x] 2.2 Define `MiniBusProcessor.ProcessAsync(ServiceBusReceivedMessage, ServiceBusMessageActions, CancellationToken)` for processing with basic settlement.
- [x] 2.3 Implement receive-side header extraction using the existing Service Bus application property to MiniBus header mapping.
- [x] 2.4 Implement message type resolution from `MiniBus.MessageType` and `MiniBus.EnclosedMessageTypes`, including clear failures for missing or unresolvable metadata.
- [x] 2.5 Extract received message identity, correlation id, and causation metadata for MiniBus processing.

## 3. Deserialization and handler invocation

- [x] 3.1 Deserialize received message bodies through `IMessageSerializer.Deserialize` using the resolved concrete message type.
- [x] 3.2 Create an adapter-owned `MiniBusContext` implementation populated from endpoint name, message id, correlation id, causation id, and headers.
- [x] 3.3 Invoke matching handlers through the existing MiniBus core handler invocation pipeline and dependency injection.
- [x] 3.4 Ensure business handlers do not need Azure Functions or Azure Service Bus trigger types.

## 4. Outgoing dispatch from inbound context

- [x] 4.1 Delegate `MiniBusContext.Send` to the existing Azure Service Bus transport dispatcher.
- [x] 4.2 Delegate `MiniBusContext.Publish` to the existing Azure Service Bus transport dispatcher.
- [x] 4.3 Delegate `MiniBusContext.Schedule` to the existing Azure Service Bus transport dispatcher.
- [x] 4.4 Preserve inbound correlation and causation metadata in outgoing headers where supported by the transport message factory.
- [x] 4.5 Fail fast with a clear error when outgoing dispatch is requested but required transport dispatch services or routes are missing.

## 5. Settlement behavior

- [x] 5.1 Complete the Service Bus message through `ServiceBusMessageActions` after successful settlement-enabled processing.
- [x] 5.2 Dead-letter the Service Bus message through `ServiceBusMessageActions` when type resolution, deserialization, handler invocation, or outgoing dispatch fails.
- [x] 5.3 Ensure failed processing does not also complete the message.
- [x] 5.4 Ensure the no-settlement overload does not complete or dead-letter messages and propagates processing failures.
- [x] 5.5 Keep dead-letter reason and description basic and avoid retry or advanced diagnostic policy behavior.

## 6. Dependency injection and sample

- [x] 6.1 Add a dependency injection extension for registering Azure Functions adapter services and options.
- [x] 6.2 Ensure the DI extension wires `MiniBusProcessor` and required adapter-owned collaborators.
- [x] 6.3 Add a minimal sample Azure Function wrapper using `ServiceBusTrigger`.
- [x] 6.4 Ensure the sample delegates directly to `MiniBusProcessor` and keeps handlers free of Azure Functions and Service Bus trigger types.

## 7. Unit tests

- [x] 7.1 Add tests verifying successful processor execution deserializes with the resolved type, invokes handlers, exposes headers/context metadata, dispatches outgoing operations where applicable, and completes the message.
- [x] 7.2 Add tests verifying the no-settlement overload processes valid messages without completing or dead-lettering.
- [x] 7.3 Add tests verifying missing message type metadata dead-letters under the settlement overload and propagates under the no-settlement overload.
- [x] 7.4 Add tests verifying unresolvable message type metadata dead-letters the message and does not invoke handlers.
- [x] 7.5 Add tests verifying deserialization failure dead-letters the message and does not complete it.
- [x] 7.6 Add tests verifying handler failure dead-letters the message and does not complete it.
- [x] 7.7 Add tests verifying outgoing dispatch failure dead-letters the message and does not complete it.
- [x] 7.8 Add tests verifying the DI extension registers `MiniBusProcessor`.

## 8. Verification

- [x] 8.1 Build the solution.
- [x] 8.2 Run `MiniBus.Core.Tests`, `MiniBus.AzureServiceBus.Tests`, and `MiniBus.AzureFunctions.Tests`.
- [x] 8.3 Review public APIs to confirm application handlers remain free of Azure Functions and Azure Service Bus trigger dependencies.
