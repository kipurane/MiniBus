# structured-processing-logging Specification

## Purpose
TBD - created by archiving change add-structured-processing-logging. Update Purpose after archive.
## Requirements
### Requirement: Processing logs use structured logging
MiniBus SHALL emit framework-level processing logs through `Microsoft.Extensions.Logging` without requiring application handler code changes or a specific logging provider.

#### Scenario: Processing start is logged
- **WHEN** MiniBus starts processing a received Service Bus message
- **THEN** it emits a structured processing-start log with a stable event id or event name and includes endpoint name, message id, correlation id when available, causation id when available, retry attempt when available, and delayed retry attempt when available

#### Scenario: Application logging providers are unchanged
- **WHEN** an application configures MiniBus processing and its own logging providers
- **THEN** MiniBus uses the application's existing `Microsoft.Extensions.Logging` configuration without requiring a MiniBus-specific sink or external logging package

### Requirement: Processing logs use correlation-aware scopes
MiniBus SHALL create a log scope for each received message processing attempt that carries correlation-aware processing metadata when available.

#### Scenario: Correlation metadata is present
- **WHEN** a received message includes MiniBus or Service Bus correlation metadata
- **THEN** every MiniBus framework log emitted for that processing attempt is inside a scope that includes `EndpointName`, `MessageId`, `CorrelationId`, and `CausationId` when those values are available

#### Scenario: Correlation metadata is absent
- **WHEN** a received message does not include correlation or causation metadata
- **THEN** MiniBus still emits processing logs in a scope containing the endpoint name and message id without failing processing

#### Scenario: Immediate retry updates scope metadata
- **WHEN** recoverability selects an immediate retry and MiniBus starts a new processing attempt with updated retry headers
- **THEN** the new processing attempt scope includes the updated retry attempt metadata

### Requirement: Diagnostic property names are stable
MiniBus SHALL use stable structured property names for processing diagnostics so tests, log queries, and future telemetry can rely on them.

#### Scenario: Core message metadata is logged
- **WHEN** MiniBus emits processing diagnostics for a received message
- **THEN** the structured state uses stable property names for endpoint name, message type, message id, correlation id, causation id, retry attempt, and delayed retry attempt

#### Scenario: Invocation metadata is logged
- **WHEN** MiniBus emits processing diagnostics for handler or saga invocation
- **THEN** the structured state uses stable property names for handler type, saga type, and saga correlation id when those values are known

#### Scenario: Outcome metadata is logged
- **WHEN** MiniBus emits processing outcome diagnostics
- **THEN** the structured state uses stable property names for processing outcome, outbox operation count when available, and dead-letter reason when available

### Requirement: Processing outcome logs are emitted once per terminal attempt outcome
MiniBus SHALL emit structured outcome logs at terminal processing attempt boundaries without duplicating final outcomes for the same attempt.

#### Scenario: Successful processing completes
- **WHEN** a received message is processed successfully and is not short-circuited as a duplicate
- **THEN** MiniBus emits one completed outcome log containing the processing outcome, endpoint name, message type, message id, correlation id when available, and outbox operation count when available

#### Scenario: Duplicate inbox message is skipped
- **WHEN** SQL inbox persistence detects that the received logical message id was already processed
- **THEN** MiniBus emits one skipped-duplicate outcome log containing the processing outcome, endpoint name, logical message id, correlation id when available, and duplicate-detection metadata available from the processing context

#### Scenario: Immediate retry is selected
- **WHEN** processing fails and recoverability selects an immediate retry
- **THEN** MiniBus emits one retried outcome log for the failed attempt containing the processing outcome, exception type, retry attempt metadata, endpoint name, message id, and correlation id when available

#### Scenario: Delayed retry is scheduled
- **WHEN** processing fails and recoverability schedules a delayed retry
- **THEN** MiniBus emits one delayed-retry-scheduled outcome log after scheduling succeeds and before completing the original received message

#### Scenario: Message is dead-lettered
- **WHEN** processing fails and recoverability selects dead-lettering
- **THEN** MiniBus emits one dead-lettered outcome log before dead-letter settlement and includes the dead-letter reason and description when available

#### Scenario: Failure propagates
- **WHEN** processing fails and MiniBus propagates the exception instead of settling the received message
- **THEN** MiniBus emits one failure outcome log containing the exception type, endpoint name, message id, correlation id when available, and processing outcome

### Requirement: Handler and saga diagnostics are emitted when known
MiniBus SHALL emit structured diagnostics for handler and saga invocation metadata when the processing pipeline can determine that metadata.

#### Scenario: Handler invocation is logged
- **WHEN** MiniBus invokes a message handler
- **THEN** it emits structured diagnostics that include the handler type, endpoint name, message type, message id, and correlation id when available

#### Scenario: Saga invocation is logged
- **WHEN** MiniBus invokes a saga for a received message
- **THEN** it emits structured diagnostics that include the saga type, saga correlation id, endpoint name, message type, message id, and correlation id when available

#### Scenario: Saga completion is logged
- **WHEN** saga processing marks saga data as completed
- **THEN** MiniBus emits structured diagnostics with a saga-completed outcome and includes saga type and saga correlation id when available

### Requirement: Outbox dispatch diagnostics are emitted when known
MiniBus SHALL emit structured diagnostics for outbox dispatch outcomes when outbox behavior runs as part of message processing.

#### Scenario: Outbox operations are captured and committed
- **WHEN** processing succeeds with SQL outbox capture enabled and outgoing operations are committed with the incoming message
- **THEN** MiniBus emits structured diagnostics containing an outbox-dispatched or outbox-committed outcome and the outbox operation count available from the processing context

#### Scenario: No outbox operations exist
- **WHEN** processing succeeds without captured outbox operations
- **THEN** MiniBus does not emit a misleading outbox-dispatched outcome

### Requirement: Structured logging is test-verifiable
MiniBus SHALL provide test coverage or test hooks that verify structured logging behavior without depending on formatted log text.

#### Scenario: Tests inspect structured state
- **WHEN** the MiniBus Azure Functions test suite verifies processing logs
- **THEN** it asserts event ids or event names, log levels, scope values, and structured property keys from captured logging state rather than rendered message strings

#### Scenario: Tests cover representative outcomes
- **WHEN** structured logging tests run
- **THEN** they cover successful processing, immediate retry, delayed retry, dead-lettering, duplicate inbox skip, handler diagnostics, saga diagnostics, and outbox diagnostics where the current pipeline supports those outcomes

