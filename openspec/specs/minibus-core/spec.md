# minibus-core Specification

## Purpose
TBD - created by archiving change add-core-message-processing. Update Purpose after archive.
## Requirements
### Requirement: Message contracts define MiniBus message intent
The core library SHALL define transport-agnostic message contracts for `IMessage`, `ICommand`, and `IEvent` so application code can express message intent without depending on Azure SDK or hosting types.

#### Scenario: Message contract markers are available to application code
- **WHEN** an application references `MiniBus.Core`
- **THEN** it can implement `IMessage`, `ICommand`, or `IEvent` on its message contract types

### Requirement: Handlers process a single message type through a common abstraction
The core library SHALL define `IHandleMessages<TMessage>` for message handlers, where `TMessage` is constrained to `IMessage`, and handlers MUST receive the message instance, a `MiniBusContext`, and a `CancellationToken`.

#### Scenario: A handler implements the MiniBus handler contract
- **WHEN** a developer creates a handler for a message type
- **THEN** the handler can implement `IHandleMessages<TMessage>` and receive the message, context, and cancellation token during invocation

### Requirement: MiniBus context exposes transport-agnostic outgoing operations and metadata
The core library SHALL define `MiniBusContext` as the handler-facing abstraction for endpoint metadata, headers, and outgoing operations such as send, publish, and schedule without exposing transport-specific types.

#### Scenario: A handler reads metadata and requests outgoing work
- **WHEN** a handler is invoked with a `MiniBusContext`
- **THEN** it can read message metadata and call transport-agnostic outgoing operations without referencing Azure Service Bus or Azure Functions APIs

### Requirement: Messages are serialized through an explicit serializer abstraction
The core library SHALL define `IMessageSerializer` with operations to serialize a message instance and deserialize a payload using an explicit message `Type`, and it SHALL provide a default `System.Text.Json` implementation.

#### Scenario: A message round-trips through the default serializer
- **WHEN** a supported message instance is serialized and deserialized through the default serializer
- **THEN** the resulting object is restored as the requested message type from UTF-8 JSON data

### Requirement: Command routing uses explicit type-to-destination mappings
The core library SHALL support explicit routing for command message types and MUST reject missing or conflicting command route definitions.

#### Scenario: A configured command route is resolved
- **WHEN** a command message type has been mapped to a destination
- **THEN** the routing registry returns that destination for the command type

#### Scenario: A command route is missing
- **WHEN** code attempts to resolve a destination for a command type that has not been mapped
- **THEN** the core library fails with a specific routing error instead of choosing a convention-based destination

#### Scenario: Conflicting command route definitions are registered
- **WHEN** the same command message type is mapped to different destinations
- **THEN** the core library fails configuration for that command route

### Requirement: Handler discovery finds MiniBus handlers in registered assemblies
The core library SHALL support discovering concrete `IHandleMessages<TMessage>` implementations from configured assemblies so they can be registered for message invocation.

#### Scenario: Concrete handlers are discovered from an assembly
- **WHEN** handler discovery runs against an assembly containing concrete MiniBus handlers
- **THEN** the discovered handler registrations include each implemented `IHandleMessages<TMessage>` contract

#### Scenario: Non-handler types are ignored during discovery
- **WHEN** handler discovery encounters types that do not implement `IHandleMessages<TMessage>` or are abstract
- **THEN** those types are not returned as handler registrations

### Requirement: Message invocation resolves and executes matching handlers through dependency injection
The core library SHALL resolve matching handlers for a message type from the application service provider and invoke each handler asynchronously with the deserialized message, `MiniBusContext`, and `CancellationToken`.

#### Scenario: A message is delivered to its registered handlers
- **WHEN** a message instance is invoked through the core handler invocation flow
- **THEN** each matching registered handler is executed asynchronously with the same message, context, and cancellation token

#### Scenario: No handlers are registered for a message type
- **WHEN** a message instance is invoked and no matching handlers are registered
- **THEN** the invocation flow completes without invoking any handlers

