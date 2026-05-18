# minibus-testing Specification

## Purpose
Defines developer testing helpers for unit testing MiniBus handlers and saga handlers without requiring Azure Functions, Azure Service Bus, SQL persistence, Azure Storage, or a real MiniBus processor host.

## Requirements
### Requirement: Testing package isolates handler tests from infrastructure
MiniBus SHALL provide a `MiniBus.Testing` package for direct handler and saga handler unit tests without requiring Azure Functions, Azure Service Bus, SQL persistence, Azure Storage, OpenTelemetry, or test-framework-specific dependencies.

#### Scenario: Application test project references testing package
- **WHEN** an application test project references `MiniBus.Testing`
- **THEN** it can create MiniBus testing helpers using only MiniBus core contracts and BCL types

#### Scenario: Testing package avoids infrastructure dependencies
- **WHEN** the `MiniBus.Testing` project is built
- **THEN** it references `MiniBus.Core` without referencing host, transport, storage, observability, Azure SDK, SQL, or test assertion packages

### Requirement: Testable context exposes configurable inbound metadata
MiniBus SHALL provide `TestableMiniBusContext` deriving from `MiniBusContext` with configurable endpoint name, message id, correlation id, optional causation id, and headers.

#### Scenario: Test uses default metadata
- **WHEN** a test creates `TestableMiniBusContext` without custom metadata
- **THEN** the context exposes deterministic non-empty endpoint name, message id, correlation id, and an empty or configurable header set

#### Scenario: Test configures metadata and headers
- **WHEN** a test creates or configures `TestableMiniBusContext` with endpoint name, message id, correlation id, causation id, and headers
- **THEN** handler code reading `MiniBusContext` receives those configured values

### Requirement: Testable context captures outgoing operations
MiniBus SHALL capture outgoing send, publish, and schedule requests made through `TestableMiniBusContext` while preserving the message object, concrete message type, and schedule due time where applicable.

#### Scenario: Handler sends command
- **WHEN** a handler calls `Send` on `TestableMiniBusContext`
- **THEN** the context records a sent command operation containing the original command object and command type

#### Scenario: Handler publishes event
- **WHEN** a handler calls `Publish` on `TestableMiniBusContext`
- **THEN** the context records a published event operation containing the original event object and event type

#### Scenario: Handler schedules message
- **WHEN** a handler calls `Schedule` on `TestableMiniBusContext`
- **THEN** the context records a scheduled message operation containing the original message object, message type, and due time

### Requirement: Captured operations are queryable by message type
MiniBus SHALL provide dependency-free helper APIs that let tests query captured sent, published, and scheduled operations by message type.

#### Scenario: Test queries operations by type
- **WHEN** a test queries captured outgoing operations for a specific command, event, or message type
- **THEN** the helper returns only matching captured operations with strongly typed message access where practical

#### Scenario: Test asks for a single operation
- **WHEN** a test uses a single-result helper for a command, event, or scheduled message type
- **THEN** the helper returns the only matching operation or fails with a clear ordinary exception when zero or multiple matches exist

### Requirement: Testing package documents direct handler testing
MiniBus SHALL document how to unit test a handler using `TestableMiniBusContext`.

#### Scenario: Developer reads testing package documentation
- **WHEN** a developer reads the `MiniBus.Testing` package documentation
- **THEN** it shows creating a testable context, invoking a handler directly, and asserting captured send, publish, or schedule operations

#### Scenario: Documentation keeps scope clear
- **WHEN** a developer reads the `MiniBus.Testing` package documentation
- **THEN** it explains that the first package scope is direct handler and saga handler testing rather than Azure Functions processor, live transport, or SQL persistence integration testing
