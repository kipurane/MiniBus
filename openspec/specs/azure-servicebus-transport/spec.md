# azure-servicebus-transport Specification

## Purpose
Defines Azure Service Bus transport dispatch for MiniBus send, publish, schedule, message envelope creation, routing, and header mapping while keeping handler-facing APIs transport agnostic.
## Requirements
### Requirement: Azure Service Bus transport package isolates Azure SDK dependencies
The Azure Service Bus transport SHALL provide MiniBus dispatch capabilities in a package that depends on `MiniBus.Core` and Azure Service Bus SDK types without requiring application handlers to reference Azure Service Bus APIs.

#### Scenario: Handler-facing APIs remain transport agnostic
- **WHEN** application handlers send, publish, or schedule messages through `MiniBusContext`
- **THEN** the handler code does not require `Azure.Messaging.ServiceBus` types

### Requirement: Transport sender abstraction supports immediate and scheduled dispatch
The Azure Service Bus transport SHALL define a sender abstraction capable of sending a Service Bus message to a queue or topic and scheduling a Service Bus message for a future enqueue time.

#### Scenario: A command is sent through the sender abstraction
- **WHEN** transport dispatch sends a command message to a resolved queue destination
- **THEN** it calls the sender abstraction with the destination entity and the created Service Bus message

#### Scenario: A message is scheduled through the sender abstraction
- **WHEN** transport dispatch schedules a message for a future due time
- **THEN** it calls the sender abstraction with the destination entity, created Service Bus message, and scheduled enqueue time

### Requirement: Commands are sent to explicitly routed queues
The Azure Service Bus transport SHALL send `ICommand` messages to queue destinations resolved from explicit command routing and MUST fail when no route exists.

#### Scenario: A routed command is sent to its queue
- **WHEN** a command type has an explicit queue route and dispatch sends that command
- **THEN** the transport sends one Service Bus message to the configured queue destination

#### Scenario: A command route is missing during send
- **WHEN** dispatch attempts to send a command type without an explicit queue route
- **THEN** the transport fails before calling the sender abstraction

### Requirement: Events are published to explicitly routed topics
The Azure Service Bus transport SHALL publish `IEvent` messages to topic destinations resolved from explicit event routing and MUST fail when no topic route exists.

#### Scenario: A routed event is published to its topic
- **WHEN** an event type has an explicit topic route and dispatch publishes that event
- **THEN** the transport sends one Service Bus message to the configured topic destination

#### Scenario: An event route is missing during publish
- **WHEN** dispatch attempts to publish an event type without an explicit topic route
- **THEN** the transport fails before calling the sender abstraction

### Requirement: Scheduled messages resolve Service Bus destinations explicitly
The Azure Service Bus transport SHALL schedule outgoing messages only when it can resolve an explicit Service Bus destination for the message type and operation.

#### Scenario: A command is scheduled to its queue
- **WHEN** a command type has an explicit queue route and dispatch schedules that command
- **THEN** the transport schedules one Service Bus message to the configured queue destination for the requested due time

#### Scenario: An event is scheduled to its topic
- **WHEN** an event type has an explicit topic route and dispatch schedules that event
- **THEN** the transport schedules one Service Bus message to the configured topic destination for the requested due time

#### Scenario: A generic message lacks a schedule destination
- **WHEN** dispatch attempts to schedule a generic `IMessage` that is neither a routed command nor a routed event and no explicit schedule destination exists
- **THEN** the transport fails before calling the sender abstraction

### Requirement: Outgoing messages use the MiniBus core serializer
The Azure Service Bus transport SHALL serialize outgoing message bodies using the configured `MiniBus.Core` `IMessageSerializer` and the concrete MiniBus message type.

#### Scenario: A command body is serialized
- **WHEN** transport dispatch creates a Service Bus message for a command
- **THEN** the body is produced by calling `IMessageSerializer.Serialize` with the command instance and command type

#### Scenario: An event body is serialized
- **WHEN** transport dispatch creates a Service Bus message for an event
- **THEN** the body is produced by calling `IMessageSerializer.Serialize` with the event instance and event type

### Requirement: Transport message factory creates Service Bus envelopes
The Azure Service Bus transport SHALL provide a transport-level message factory that creates `ServiceBusMessage` envelopes from MiniBus messages, message types, serialized bodies, and MiniBus headers.

#### Scenario: A Service Bus message is created from MiniBus metadata
- **WHEN** the message factory receives a MiniBus message instance, concrete message type, and headers
- **THEN** it returns a `ServiceBusMessage` containing the serialized body and mapped metadata

#### Scenario: Message type metadata is included
- **WHEN** the message factory creates a Service Bus message
- **THEN** the resulting message includes MiniBus message type metadata in application properties

### Requirement: MiniBus headers map to Service Bus application properties
The Azure Service Bus transport SHALL map outgoing MiniBus headers to Service Bus application properties using the MiniBus header names and string values.

#### Scenario: Headers are copied to application properties
- **WHEN** a Service Bus message is created with MiniBus headers
- **THEN** each supported MiniBus header is present in `ApplicationProperties` with the same key and value

#### Scenario: Core Service Bus system properties mirror MiniBus headers
- **WHEN** headers include MiniBus message id, correlation id, content type, or message type metadata
- **THEN** the Service Bus message system properties mirror those values where the Azure SDK exposes matching properties

### Requirement: Service Bus application properties map back to MiniBus headers
The Azure Service Bus transport SHALL provide mapping from Service Bus application properties back to a MiniBus header dictionary for receive-side adapters.

#### Scenario: Application properties become MiniBus headers
- **WHEN** a Service Bus message contains application properties with MiniBus header names
- **THEN** the mapper returns a MiniBus header dictionary containing those headers as strings

#### Scenario: Non-string primitive application properties are converted
- **WHEN** a Service Bus application property value is a supported primitive value other than string
- **THEN** the mapper converts the value to a string representation for the MiniBus header dictionary

### Requirement: Transport behavior is covered by unit tests without live Service Bus infrastructure
The Azure Service Bus transport SHALL include unit tests for routing, dispatch, scheduling, serialization, message factory behavior, and header mapping without requiring a live Azure Service Bus namespace.

#### Scenario: Dispatch tests use mocked abstractions
- **WHEN** unit tests validate command send, event publish, or scheduled message dispatch
- **THEN** they verify calls to the sender abstraction rather than connecting to Azure Service Bus

#### Scenario: Message creation tests inspect SDK message objects
- **WHEN** unit tests validate body and header mapping behavior
- **THEN** they inspect created `ServiceBusMessage` instances directly

### Requirement: Azure Service Bus transport dispatches persisted outbox operations
The Azure Service Bus transport SHALL provide dispatch behavior that can be used by the SQL outbox dispatcher to send persisted outgoing operations.

#### Scenario: Persisted command operation is sent
- **WHEN** the SQL outbox dispatcher passes a persisted command send operation to the Azure Service Bus transport
- **THEN** the transport sends the command to its resolved queue destination

#### Scenario: Persisted event operation is published
- **WHEN** the SQL outbox dispatcher passes a persisted event publish operation to the Azure Service Bus transport
- **THEN** the transport publishes the event to its resolved topic destination

#### Scenario: Persisted scheduled operation is scheduled
- **WHEN** the SQL outbox dispatcher passes a persisted scheduled operation to the Azure Service Bus transport
- **THEN** the transport schedules the message for the stored due time

### Requirement: Persisted outbox dispatch preserves MiniBus headers
The Azure Service Bus transport SHALL preserve stored MiniBus headers when dispatching operations loaded from the SQL outbox.

#### Scenario: Stored headers become Service Bus properties
- **WHEN** a persisted outbox operation is dispatched through Azure Service Bus
- **THEN** the stored MiniBus headers are mapped to Service Bus application properties

#### Scenario: Stored correlation metadata is preserved
- **WHEN** a persisted outbox operation contains correlation and causation headers
- **THEN** the dispatched Service Bus message contains those values in the mapped metadata
