# saga-timeouts Specification

## Purpose
Defines MiniBus saga timeout behavior using transport-independent timeout contracts, saga-facing request APIs, explicit saga correlation, existing MiniBus scheduling, SQL outbox capture, Azure Service Bus scheduled dispatch, and verification.

## Requirements
### Requirement: Saga contracts expose timeout messages
MiniBus SHALL provide transport-independent saga timeout contracts so applications can model timeout messages without depending on Azure Functions, Azure Service Bus, or SQL APIs.

#### Scenario: Application defines a timeout message
- **WHEN** an application references MiniBus saga contracts
- **THEN** it can define a timeout message as a normal MiniBus message with saga timeout intent

#### Scenario: Timeout message remains transport independent
- **WHEN** a saga timeout message type is compiled
- **THEN** it does not require Azure Service Bus, Azure Functions, or SQL client types

### Requirement: Sagas can request timeout messages
MiniBus SHALL provide saga-facing APIs or conventions for requesting timeout messages at a future due time.

#### Scenario: Saga requests timeout at absolute due time
- **WHEN** a saga handler requests a timeout message with a `DateTimeOffset` due time
- **THEN** MiniBus schedules that timeout message for the requested due time through the current `MiniBusContext`

#### Scenario: Saga requests timeout after delay
- **WHEN** a saga handler requests a timeout message with a relative delay
- **THEN** MiniBus computes a future due time and schedules that timeout message through the current `MiniBusContext`

#### Scenario: Timeout request validates inputs
- **WHEN** a saga requests a timeout with a null timeout message or invalid timing input
- **THEN** MiniBus rejects the request before dispatching or storing an outgoing operation

### Requirement: Timeout messages use existing MiniBus scheduling
MiniBus SHALL schedule saga timeout messages as normal MiniBus scheduled messages using the configured transport and persistence behavior.

#### Scenario: SQL outbox captures timeout schedule
- **WHEN** a saga requests a timeout during SQL outbox-enabled processing
- **THEN** MiniBus stores a pending SQL outbox `Schedule` operation containing the timeout message, concrete message type, headers, and due time

#### Scenario: Timeout schedule commits atomically with saga state
- **WHEN** a SQL-backed saga handler requests a timeout and saga handling succeeds
- **THEN** MiniBus commits the saga state change, inbox state, and timeout schedule in the same successful persistence flow

#### Scenario: Timeout schedule is not committed after failure
- **WHEN** a saga handler requests a timeout and then fails before successful processing completes
- **THEN** MiniBus does not commit the requested timeout schedule as processed outgoing work

#### Scenario: Direct dispatch schedules through transport
- **WHEN** a saga requests a timeout while SQL outbox capture is disabled
- **THEN** MiniBus schedules the timeout message directly through the configured Azure Service Bus transport dispatcher

### Requirement: Timeout messages correlate to saga state explicitly
MiniBus SHALL process timeout messages through the existing saga correlation rules and MUST NOT create saga state for timeout messages unless the saga explicitly configures that message type as a starting message.

#### Scenario: Timeout correlates to existing saga
- **WHEN** a timeout message arrives with a configured continuing correlation mapping and matching saga state exists
- **THEN** MiniBus loads the saga state and invokes the saga timeout handler

#### Scenario: Timeout has no matching saga
- **WHEN** a timeout message arrives with a configured continuing correlation mapping and no matching saga state exists
- **THEN** MiniBus does not create new saga state or invoke the saga timeout handler

#### Scenario: Timeout maps as starting message
- **WHEN** a saga explicitly configures a timeout message type as a starting message
- **THEN** MiniBus may create new saga state using the existing starting-message behavior

#### Scenario: Timeout correlation is missing
- **WHEN** a timeout message has no configured saga correlation mapping
- **THEN** MiniBus fails with the same clear saga mapping error used for other unmapped saga messages

### Requirement: Timeout handling preserves saga processing semantics
MiniBus SHALL handle delivered timeout messages with the same invocation, persistence, recoverability, and completed-saga behavior as other saga messages.

#### Scenario: Timeout handler saves saga state after success
- **WHEN** a saga timeout handler updates saga data and completes successfully
- **THEN** MiniBus saves the updated saga state using the configured saga persistence provider

#### Scenario: Timeout handler failure preserves prior saga state
- **WHEN** a saga timeout handler throws an exception
- **THEN** MiniBus does not save the failed timeout attempt's saga state changes

#### Scenario: Timeout arrives for completed saga
- **WHEN** a timeout message correlates to saga state that is already completed
- **THEN** MiniBus does not invoke the saga timeout handler

#### Scenario: Timeout persistence conflict flows through recoverability
- **WHEN** timeout handling encounters a saga persistence or optimistic concurrency failure
- **THEN** MiniBus reports processing failure through the existing recoverability pipeline

### Requirement: Timeout scheduling preserves MiniBus metadata
MiniBus SHALL preserve MiniBus message metadata for scheduled timeout messages using the same metadata semantics as other outgoing scheduled operations.

#### Scenario: Timeout schedule preserves correlation metadata
- **WHEN** a saga requests a timeout while handling a received message
- **THEN** the scheduled timeout operation preserves the current MiniBus correlation id

#### Scenario: Timeout schedule records causation metadata
- **WHEN** a saga requests a timeout while handling a received message
- **THEN** the scheduled timeout operation identifies the received message as the causation id

#### Scenario: Persisted timeout replay uses deterministic outgoing message id
- **WHEN** a persisted timeout schedule operation is retried by SQL outbox dispatch
- **THEN** every dispatch attempt uses the same deterministic outgoing message id for that operation

### Requirement: Timeout behavior is documented and tested
MiniBus SHALL document and test saga timeout request, scheduling, correlation, dispatch, and persistence behavior.

#### Scenario: Documentation shows timeout workflow
- **WHEN** a developer reads the saga documentation or sample
- **THEN** it shows defining a timeout message, requesting the timeout from a saga, mapping timeout correlation, and handling the delivered timeout

#### Scenario: Unit tests cover timeout request and correlation
- **WHEN** the normal test suite runs
- **THEN** it verifies timeout request APIs, timeout correlation behavior, completed-saga behavior, and direct dispatch behavior without requiring live Azure Service Bus infrastructure

#### Scenario: SQL integration tests cover timeout outbox capture
- **WHEN** SQL Server-backed integration tests run through Testcontainers or a configured test connection string
- **THEN** they verify saga timeout schedules are persisted as SQL outbox scheduled operations with due time and metadata

#### Scenario: Transport tests cover timeout scheduled dispatch
- **WHEN** Azure Service Bus transport tests dispatch a persisted timeout schedule operation
- **THEN** they verify the transport schedules the timeout message to the configured destination for the stored due time
