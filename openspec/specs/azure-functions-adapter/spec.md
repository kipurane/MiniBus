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

### Requirement: Azure Functions sample shows complete adapter registration
The Azure Functions adapter documentation and samples SHALL include a buildable isolated worker sample showing complete adapter registration.

#### Scenario: Sample registers adapter services
- **WHEN** a developer reads the buildable Function App sample
- **THEN** it shows `AddMiniBusAzureFunctions` registration with endpoint and recoverability options

#### Scenario: Sample keeps trigger wrapper thin
- **WHEN** a developer reads the sample Service Bus trigger function
- **THEN** the function delegates directly to `MiniBusProcessor.ProcessAsync`

### Requirement: Adapter behavior is covered by unit tests without live Azure resources
The Azure Functions adapter SHALL include unit tests for processor behavior without requiring a live Azure Functions host or Azure Service Bus namespace.

#### Scenario: Processor success path is unit tested
- **WHEN** unit tests process a valid received message
- **THEN** they verify deserialization, handler invocation, header availability, outgoing dispatch where applicable, and completion behavior

#### Scenario: Processor failure path is unit tested
- **WHEN** unit tests process an invalid message or a message whose handler or outgoing dispatch fails
- **THEN** they verify settlement follows the configured recoverability outcome and failed messages are not completed unless a delayed retry copy has been scheduled

### Requirement: Azure Functions processing supports SQL inbox checks
The Azure Functions adapter SHALL use the configured MiniBus inbox service during processing when SQL inbox/outbox persistence is enabled.

#### Scenario: Duplicate message is completed without handler invocation
- **WHEN** settlement-enabled processing receives a message already recorded in the inbox for the endpoint
- **THEN** the processor does not invoke handlers and completes the received Service Bus message

#### Scenario: Duplicate message without settlement does not invoke handlers
- **WHEN** no-settlement processing receives a message already recorded in the inbox for the endpoint
- **THEN** the processor does not invoke handlers and returns without calling settlement APIs

### Requirement: Azure Functions processing commits SQL outbox before completion
The Azure Functions adapter SHALL commit SQL inbox and outbox state before completing a received Service Bus message when SQL inbox/outbox persistence is enabled.

#### Scenario: SQL commit succeeds before completion
- **WHEN** settlement-enabled processing handles a message successfully with SQL persistence enabled
- **THEN** the processor commits inbox and outbox state before calling `CompleteMessageAsync`

#### Scenario: SQL commit failure prevents completion
- **WHEN** settlement-enabled processing handles a message but SQL inbox/outbox commit fails
- **THEN** the processor does not call `CompleteMessageAsync` and propagates the failure to the caller

### Requirement: Azure Functions processing captures outgoing operations when SQL outbox is enabled
The Azure Functions adapter SHALL capture outgoing operations requested through `MiniBusContext` during handler execution instead of directly dispatching them when SQL outbox persistence is enabled.

#### Scenario: Handler requests outgoing work
- **WHEN** a handler calls `Send`, `Publish`, or `Schedule` during SQL-backed processing
- **THEN** the processor captures the outgoing operation for SQL outbox persistence

#### Scenario: SQL outbox disabled preserves direct dispatch
- **WHEN** a handler calls `Send`, `Publish`, or `Schedule` and SQL outbox persistence is not enabled
- **THEN** the processor keeps the existing direct dispatch behavior

### Requirement: Azure Functions processor delegates to internal pipeline
The Azure Functions adapter SHALL keep the public `MiniBusProcessor` processing overloads while delegating processing orchestration to the internal MiniBus processing pipeline.

#### Scenario: Public processor overloads remain available
- **WHEN** an application references the Azure Functions adapter
- **THEN** `MiniBusProcessor.ProcessAsync(ServiceBusReceivedMessage, CancellationToken)`, `MiniBusProcessor.ProcessAsync(ServiceBusReceivedMessage, ServiceBusMessageActions, CancellationToken)`, and `MiniBusProcessor.ProcessAsync(ServiceBusReceivedMessage, IMiniBusMessageActions, CancellationToken)` remain available

#### Scenario: No-settlement overload uses pipeline
- **WHEN** the no-settlement overload processes a received Service Bus message
- **THEN** it uses the internal pipeline and preserves existing behavior for deserialization, handler invocation, saga invocation, direct dispatch, SQL inbox/outbox, and failure propagation

#### Scenario: Settlement overload uses pipeline
- **WHEN** the settlement-enabled overload processes a received Service Bus message
- **THEN** it uses the internal pipeline and preserves existing behavior for successful completion, immediate retries, delayed retry scheduling, dead-lettering, duplicate inbox completion, and persistence commit failures

### Requirement: Azure Functions settlement decisions are represented explicitly
The Azure Functions adapter SHALL represent settlement outcomes explicitly inside the pipeline before invoking Azure Service Bus settlement APIs.

#### Scenario: Processing succeeds
- **WHEN** settlement-enabled processing succeeds
- **THEN** the pipeline records a complete-message settlement decision and the adapter completes the received Service Bus message

#### Scenario: Delayed retry is selected
- **WHEN** recoverability selects a delayed retry
- **THEN** the pipeline records that a retry copy must be scheduled and the original received message completed

#### Scenario: Dead-letter is selected
- **WHEN** recoverability selects a dead-letter outcome
- **THEN** the pipeline records the dead-letter reason and description used by the adapter settlement action

### Requirement: Azure Functions processing resolves claim-checks before deserialization
The Azure Functions adapter SHALL resolve MiniBus claim-check payload references before deserializing the received message body.

#### Scenario: Claim-check resolution precedes deserialization
- **WHEN** `MiniBusProcessor` receives a Service Bus message with valid MiniBus claim-check metadata
- **THEN** it loads the referenced payload body before invoking the configured MiniBus message serializer

#### Scenario: Message type resolution is preserved
- **WHEN** `MiniBusProcessor` resolves a claim-checked message
- **THEN** it uses the MiniBus message type headers from the received Service Bus message to deserialize the restored payload body

#### Scenario: Handler metadata is preserved
- **WHEN** a handler processes a resolved claim-checked message
- **THEN** its `MiniBusContext` exposes the original MiniBus headers including message id, correlation id, causation id, content type, and claim-check metadata

### Requirement: Azure Functions processing handles claim-check resolution failures through recoverability
The Azure Functions adapter SHALL treat claim-check resolution failures as processing failures before handler or saga invocation.

#### Scenario: Missing claim-check payload enters recoverability
- **WHEN** `MiniBusProcessor` cannot find a referenced claim-check payload
- **THEN** processing fails before deserialization and existing recoverability behavior decides whether to retry, delay, dead-letter, or propagate

#### Scenario: Invalid claim-check metadata enters recoverability
- **WHEN** `MiniBusProcessor` receives malformed or unsupported claim-check metadata
- **THEN** processing fails before deserialization and existing recoverability behavior decides whether to retry, delay, dead-letter, or propagate

#### Scenario: Inline messages continue through existing path
- **WHEN** `MiniBusProcessor` receives a Service Bus message without MiniBus claim-check metadata
- **THEN** it deserializes the received body using the existing inline processing path

### Requirement: Azure Functions processing supplies audit metadata
The Azure Functions adapter SHALL supply received Service Bus metadata needed by MiniBus audit records without exposing Azure Functions or Azure Service Bus types to handlers.

#### Scenario: Service Bus metadata is available for audit
- **WHEN** a Service Bus trigger message is processed through `MiniBusProcessor`
- **THEN** audit writing can include Service Bus message id, correlation id, subject when available, content type when available, delivery count when available, enqueued timestamp when available, and mapped MiniBus headers

#### Scenario: Endpoint metadata is available for audit
- **WHEN** a Service Bus trigger message is processed through `MiniBusProcessor`
- **THEN** audit writing can include the configured MiniBus endpoint name and any received source metadata already available to the adapter

#### Scenario: Handler-facing contracts remain unchanged
- **WHEN** audit writing is enabled for Azure Functions processing
- **THEN** handlers continue to receive only the MiniBus message instance, `MiniBusContext`, and `CancellationToken`

### Requirement: Azure Functions processing audits settlement outcomes before settlement
The Azure Functions adapter SHALL write audit records before applying final settlement actions for auditable settlement-enabled outcomes.

#### Scenario: Successful processing is audited before completion
- **WHEN** settlement-enabled processing succeeds and an audit writer is configured
- **THEN** MiniBus writes the audit record before calling `CompleteMessageAsync`

#### Scenario: Duplicate processing is audited before completion
- **WHEN** settlement-enabled processing skips a duplicate message and an audit writer is configured
- **THEN** MiniBus writes the audit record before calling `CompleteMessageAsync`

#### Scenario: Delayed retry is audited before completing original
- **WHEN** recoverability schedules a delayed retry copy and an audit writer is configured
- **THEN** MiniBus writes the audit record after the retry copy is scheduled and before completing the original received message

#### Scenario: Dead-letter is audited before dead-letter settlement
- **WHEN** recoverability selects dead-letter and an audit writer is configured
- **THEN** MiniBus writes the audit record before calling `DeadLetterMessageAsync`

### Requirement: Azure Functions processing preserves existing behavior when audit is disabled
The Azure Functions adapter SHALL preserve existing no-settlement and settlement-enabled behavior when no audit writer is configured.

#### Scenario: Audit disabled for no-settlement processing
- **WHEN** no-settlement processing runs without an audit writer
- **THEN** MiniBus preserves existing deserialization, handler invocation, saga invocation, direct dispatch, SQL inbox/outbox, and failure propagation behavior

#### Scenario: Audit disabled for settlement processing
- **WHEN** settlement-enabled processing runs without an audit writer
- **THEN** MiniBus preserves existing completion, delayed retry scheduling, dead-lettering, duplicate inbox completion, persistence commit failure, and propagation behavior

### Requirement: Azure Functions documentation identifies supported wrapper models
MiniBus Azure Functions documentation SHALL describe manual Service Bus trigger wrappers as a supported Azure Functions integration model and SHALL describe source-generated wrappers as an optional integration model when the source generator package is referenced.

#### Scenario: Developer reads Azure Functions adapter documentation
- **WHEN** a developer reads the Azure Functions adapter documentation
- **THEN** it shows a thin manual Azure Function wrapper using `ServiceBusTrigger` and delegating to `MiniBusProcessor.ProcessAsync`

#### Scenario: Developer reads generated wrapper documentation
- **WHEN** a developer reads Azure Functions adapter documentation after generated wrappers are available
- **THEN** it shows how to declare generated wrappers and how to write the equivalent manual wrapper

#### Scenario: Developer chooses not to use generation
- **WHEN** a developer does not reference the source generator package
- **THEN** the documentation still provides the manual wrapper setup path

#### Scenario: Developer configures the adapter
- **WHEN** a developer follows the Azure Functions adapter documentation
- **THEN** it shows adapter registration, endpoint options, recoverability options, and the related transport/persistence registrations needed by the documented setup path

### Requirement: Azure Functions adapter supports optional generated wrappers
MiniBus Azure Functions integration SHALL support source-generated Service Bus trigger wrappers as an optional integration model that delegates to the existing `MiniBusProcessor` processing API.

#### Scenario: Generated wrapper delegates to adapter processor
- **WHEN** a generated Azure Functions Service Bus trigger wrapper receives a message
- **THEN** it delegates processing and settlement to `MiniBusProcessor.ProcessAsync(ServiceBusReceivedMessage, ServiceBusMessageActions, CancellationToken)`

#### Scenario: Manual wrapper remains supported
- **WHEN** an application uses a manually written Azure Functions Service Bus trigger wrapper
- **THEN** the wrapper remains a supported integration model and can delegate to the same `MiniBusProcessor` overloads

### Requirement: Azure Functions sample shows local emulator execution path
The Azure Functions adapter documentation and samples SHALL include a runnable Billing reference path that processes Service Bus emulator messages through the isolated-worker Functions adapter.

#### Scenario: Emulator-backed sample uses Functions wrappers
- **WHEN** a developer runs the local Billing reference workflow
- **THEN** inbound emulator messages enter MiniBus through Azure Functions Service Bus trigger wrappers

#### Scenario: Runnable wrappers stay thin
- **WHEN** a developer reads the runnable Billing sample wrappers
- **THEN** each wrapper delegates trigger message processing and settlement directly to `MiniBusProcessor.ProcessAsync`

