# source-generated-function-wrappers Specification

## Purpose
Defines MiniBus Azure Functions source-generated Service Bus trigger wrappers that keep the generated Functions code thin and delegate processing to the existing Azure Functions adapter.

## Requirements
### Requirement: Applications declare generated Service Bus trigger wrappers
MiniBus SHALL provide a source-generator declaration API that lets a consuming Azure Functions application declare generated MiniBus Service Bus trigger wrappers at compile time.

#### Scenario: Queue trigger wrapper is declared
- **WHEN** an application declares a MiniBus-generated queue trigger wrapper with a function name, queue name, and connection setting name
- **THEN** the source generator produces a Service Bus queue trigger wrapper for that declaration

#### Scenario: Topic subscription trigger wrapper is declared
- **WHEN** an application declares a MiniBus-generated topic subscription trigger wrapper with a function name, topic name, subscription name, and connection setting name
- **THEN** the source generator produces a Service Bus topic subscription trigger wrapper for that declaration

### Requirement: Generated wrappers delegate to MiniBusProcessor
Generated Azure Functions wrappers SHALL inject `MiniBusProcessor` and delegate received Service Bus messages to the existing settlement-enabled processor overload.

#### Scenario: Generated queue function runs
- **WHEN** a generated queue trigger function receives a `ServiceBusReceivedMessage`, `ServiceBusMessageActions`, and `CancellationToken`
- **THEN** it calls `MiniBusProcessor.ProcessAsync(ServiceBusReceivedMessage, ServiceBusMessageActions, CancellationToken)`

#### Scenario: Generated topic function runs
- **WHEN** a generated topic subscription trigger function receives a `ServiceBusReceivedMessage`, `ServiceBusMessageActions`, and `CancellationToken`
- **THEN** it calls `MiniBusProcessor.ProcessAsync(ServiceBusReceivedMessage, ServiceBusMessageActions, CancellationToken)`

### Requirement: Generated wrappers use Azure Functions isolated worker trigger attributes
Generated wrappers SHALL emit Azure Functions isolated worker-compatible function methods with `[Function]` and `[ServiceBusTrigger]` attributes.

#### Scenario: Queue wrapper source is generated
- **WHEN** the generator emits a queue trigger wrapper
- **THEN** the generated function method has a `[Function]` attribute using the declared function name and a `[ServiceBusTrigger]` attribute using the declared queue name and connection setting name

#### Scenario: Topic wrapper source is generated
- **WHEN** the generator emits a topic subscription trigger wrapper
- **THEN** the generated function method has a `[Function]` attribute using the declared function name and a `[ServiceBusTrigger]` attribute using the declared topic name, subscription name, and connection setting name

### Requirement: Generated source is deterministic and inspectable
The source generator SHALL produce deterministic generated type names, namespaces, method names, and source hint names from valid declarations.

#### Scenario: Same declarations are compiled repeatedly
- **WHEN** the same valid wrapper declarations are compiled multiple times
- **THEN** the generator emits equivalent generated source with stable type names, method names, and source hint names

#### Scenario: Generated source is inspected
- **WHEN** a developer inspects generated source for a valid wrapper declaration
- **THEN** the source clearly shows the trigger attributes, injected `MiniBusProcessor`, and `ProcessAsync` delegation

### Requirement: Invalid wrapper declarations produce diagnostics
The source generator SHALL report compile-time diagnostics for declarations that cannot produce valid Azure Functions wrappers.

#### Scenario: Required trigger metadata is empty
- **WHEN** a wrapper declaration has an empty function name, connection setting name, queue name, topic name, or subscription name required by that declaration kind
- **THEN** the generator reports a diagnostic describing the invalid declaration

#### Scenario: Function names are duplicated
- **WHEN** multiple wrapper declarations use the same function name
- **THEN** the generator reports a diagnostic describing the duplicate function name

#### Scenario: Invalid declaration is compiled
- **WHEN** a wrapper declaration has generator diagnostics that prevent valid source generation
- **THEN** the generator does not emit a wrapper for that invalid declaration

### Requirement: Source generation is optional
MiniBus SHALL allow applications to keep using manual Azure Functions wrappers without referencing or using the source generator package.

#### Scenario: Application uses manual wrappers only
- **WHEN** an application references `MiniBus.AzureFunctions` and writes manual Service Bus trigger wrappers
- **THEN** the application can build and run without referencing the source generator package

#### Scenario: Application opts into generated wrappers
- **WHEN** an application references the source generator package and declares generated wrappers
- **THEN** generated wrappers coexist with any manual wrappers in the same application

### Requirement: Generator behavior is covered by compile-time tests
MiniBus SHALL include tests that compile source-generator declarations and verify generated wrapper source and diagnostics.

#### Scenario: Valid declarations are tested
- **WHEN** generator tests compile valid queue and topic subscription declarations
- **THEN** the tests verify generated source contains the expected Azure Functions attributes and `MiniBusProcessor` delegation

#### Scenario: Invalid declarations are tested
- **WHEN** generator tests compile invalid declarations
- **THEN** the tests verify the expected diagnostics are reported
