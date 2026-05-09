# azure-functions-adapter Specification

## Purpose
Defines Azure Functions isolated worker integration for processing Azure Service Bus trigger messages through MiniBus while keeping host and transport APIs out of business handlers.
## Requirements
### Requirement: Azure Functions adapter package isolates hosting concerns
The Azure Functions adapter SHALL provide isolated worker Service Bus trigger integration while keeping Azure Functions and Azure Service Bus trigger types out of business handlers.

#### Scenario: Handlers remain independent of Functions trigger APIs
- **WHEN** a message is processed through the Azure Functions adapter
- **THEN** the invoked handler receives only the MiniBus message instance, `MiniBusContext`, and `CancellationToken`

### Requirement: MiniBusProcessor supports processing without settlement
The Azure Functions adapter SHALL expose `MiniBusProcessor.ProcessAsync(ServiceBusReceivedMessage, CancellationToken)` for processing a received Service Bus message without completing or dead-lettering it.

#### Scenario: Message is processed without settlement actions
- **WHEN** the no-settlement overload receives a valid Service Bus message
- **THEN** it deserializes the message, invokes matching handlers, and does not call Service Bus settlement APIs

#### Scenario: Processing failure without settlement actions propagates
- **WHEN** the no-settlement overload encounters an unrecoverable processing failure
- **THEN** it propagates the failure to the caller

### Requirement: MiniBusProcessor supports processing with settlement
The Azure Functions adapter SHALL expose `MiniBusProcessor.ProcessAsync(ServiceBusReceivedMessage, ServiceBusMessageActions, CancellationToken)` for processing a received Service Bus message with basic settlement.

#### Scenario: Manual function wrapper delegates to MiniBusProcessor
- **WHEN** an Azure Function receives a Service Bus trigger message and `ServiceBusMessageActions`
- **THEN** the wrapper can delegate processing and settlement to `MiniBusProcessor.ProcessAsync`

### Requirement: Received Service Bus messages are adapted into MiniBus processing input
The Azure Functions adapter SHALL convert a `ServiceBusReceivedMessage` into MiniBus processing input by reading its body, message type metadata, message identity, correlation metadata, and application property headers.

#### Scenario: Service Bus message metadata is read
- **WHEN** `MiniBusProcessor` receives a Service Bus message with MiniBus metadata
- **THEN** it extracts the body, headers, message id, correlation id, and message type information for MiniBus processing

### Requirement: Service Bus application properties map to MiniBus headers
The Azure Functions adapter SHALL map Service Bus application properties to MiniBus headers using the MiniBus Service Bus header mapping behavior.

#### Scenario: Application properties are available as handler headers
- **WHEN** a Service Bus trigger message contains MiniBus application properties
- **THEN** handlers invoked by the processor can read those values from `MiniBusContext.Headers`

### Requirement: Message type metadata drives deserialization
The Azure Functions adapter SHALL resolve the concrete MiniBus message type from MiniBus message type metadata before deserializing the message body, and MUST fail processing when the type cannot be resolved.

#### Scenario: Message type is resolved from metadata
- **WHEN** a received message contains resolvable MiniBus message type metadata
- **THEN** the processor uses that type for deserialization

#### Scenario: Message type metadata is missing
- **WHEN** a received message does not contain MiniBus message type metadata
- **THEN** the processor treats the message as an unrecoverable processing failure

#### Scenario: Message type metadata cannot be resolved
- **WHEN** a received message contains message type metadata that cannot be resolved to a `Type`
- **THEN** the processor treats the message as an unrecoverable processing failure

### Requirement: Message body deserialization uses MiniBus core serializer
The Azure Functions adapter SHALL deserialize received message bodies through the configured `MiniBus.Core` `IMessageSerializer` using the resolved concrete message type.

#### Scenario: Message body is deserialized with explicit type
- **WHEN** the processor has resolved the message type for a received message
- **THEN** it calls `IMessageSerializer.Deserialize` with the Service Bus message body and resolved type

### Requirement: Handlers are invoked through MiniBus core
The Azure Functions adapter SHALL invoke matching handlers through the existing MiniBus core handler invocation pipeline using dependency injection and the constructed `MiniBusContext`.

#### Scenario: Deserialized message is delivered to handlers
- **WHEN** a received message is successfully deserialized
- **THEN** the processor invokes each matching MiniBus handler with the message, context, and cancellation token

### Requirement: MiniBusContext is populated from received message metadata
The Azure Functions adapter SHALL provide a `MiniBusContext` during handler invocation that exposes endpoint name, message id, correlation id, causation id when available, and mapped MiniBus headers.

#### Scenario: Handler reads inbound context metadata
- **WHEN** a handler is invoked by the Azure Functions adapter
- **THEN** it can read endpoint and message metadata from `MiniBusContext`

### Requirement: Outgoing operations dispatch through Azure Service Bus transport
The Azure Functions adapter SHALL dispatch outgoing `Send`, `Publish`, and `Schedule` operations requested through the inbound `MiniBusContext` by delegating to the existing Azure Service Bus transport.

#### Scenario: Handler sends command during processing
- **WHEN** a handler calls `MiniBusContext.Send` while processing a Service Bus trigger message
- **THEN** the adapter delegates command dispatch to the Azure Service Bus transport

#### Scenario: Handler publishes event during processing
- **WHEN** a handler calls `MiniBusContext.Publish` while processing a Service Bus trigger message
- **THEN** the adapter delegates event publishing to the Azure Service Bus transport

#### Scenario: Handler schedules message during processing
- **WHEN** a handler calls `MiniBusContext.Schedule` while processing a Service Bus trigger message
- **THEN** the adapter delegates scheduled dispatch to the Azure Service Bus transport

### Requirement: Successful processing completes the Service Bus message
The Azure Functions adapter SHALL complete the received Service Bus message after successful processing when settlement actions are supplied.

#### Scenario: Message completes after handler success
- **WHEN** the settlement overload deserializes a message, invokes matching handlers, and dispatches outgoing operations successfully
- **THEN** it calls `CompleteMessageAsync` for the received Service Bus message

### Requirement: Settlement-enabled failures follow recoverability decisions
The Azure Functions adapter SHALL settle failed processing according to the configured recoverability decision when settlement actions are supplied. It SHALL dead-letter the received Service Bus message only when recoverability chooses a dead-letter outcome, such as when no configured retries remain.

#### Scenario: Deserialization failure dead-letters message
- **WHEN** message deserialization fails during settlement-enabled processing and recoverability chooses a dead-letter outcome
- **THEN** the processor calls `DeadLetterMessageAsync` for the received Service Bus message

#### Scenario: Handler failure dead-letters message
- **WHEN** a handler throws during settlement-enabled processing and recoverability chooses a dead-letter outcome
- **THEN** the processor calls `DeadLetterMessageAsync` for the received Service Bus message

#### Scenario: Outgoing dispatch failure dead-letters message
- **WHEN** an outgoing operation fails during settlement-enabled processing and recoverability chooses a dead-letter outcome
- **THEN** the processor calls `DeadLetterMessageAsync` for the received Service Bus message

#### Scenario: Failure with retries remaining is not dead-lettered
- **WHEN** settlement-enabled processing fails and recoverability chooses an immediate or delayed retry outcome
- **THEN** the processor does not dead-letter the received Service Bus message for that failure

### Requirement: Azure Functions integration is registered through dependency injection
The Azure Functions adapter SHALL provide a dependency injection extension for registering adapter services needed by manual Azure Function wrappers.

#### Scenario: Function app registers MiniBus Azure Functions integration
- **WHEN** an isolated worker application calls the adapter registration extension
- **THEN** `MiniBusProcessor` and required adapter services are available from dependency injection

### Requirement: Minimal Service Bus trigger sample is provided
The Azure Functions adapter SHALL include a minimal sample Azure Function wrapper using `ServiceBusTrigger` and delegating directly to `MiniBusProcessor`.

#### Scenario: Sample wrapper delegates directly to processor
- **WHEN** a developer reads the sample wrapper
- **THEN** it shows a thin Azure Function class receiving Service Bus trigger arguments and calling `MiniBusProcessor.ProcessAsync`

### Requirement: Adapter behavior is covered by unit tests without live Azure resources
The Azure Functions adapter SHALL include unit tests for processor behavior without requiring a live Azure Functions host or Azure Service Bus namespace.

#### Scenario: Processor success path is unit tested
- **WHEN** unit tests process a valid received message
- **THEN** they verify deserialization, handler invocation, header availability, outgoing dispatch where applicable, and completion behavior

#### Scenario: Processor failure path is unit tested
- **WHEN** unit tests process an invalid message or a message whose handler or outgoing dispatch fails
- **THEN** they verify settlement follows the configured recoverability outcome and failed messages are not completed unless a delayed retry copy has been scheduled
