# core-processing-pipeline Specification

## Purpose
TBD - created by archiving change add-core-processing-pipeline. Update Purpose after archive.
## Requirements
### Requirement: Processing uses ordered pipeline behaviors
MiniBus SHALL process received messages through an ordered set of explicit pipeline behaviors for the current processing responsibilities.

#### Scenario: Behaviors execute in configured order
- **WHEN** the processor handles a received message
- **THEN** pipeline behaviors execute in the order required for metadata adaptation, message type resolution, deserialization, persistence checks, handler invocation, saga invocation, persistence commit, recoverability, and settlement

#### Scenario: Behavior boundaries are testable
- **WHEN** the test suite exercises processing behavior
- **THEN** it can verify individual behavior responsibilities without requiring a live Azure Functions host or Azure Service Bus namespace

### Requirement: Pipeline context carries processing state
MiniBus SHALL provide an internal pipeline context that carries received message metadata, headers, resolved message type, deserialized payload, handler-facing context, persistence state, outgoing operations, recoverability state, saga state, and settlement decisions.

#### Scenario: Message metadata is carried through the pipeline
- **WHEN** a received message enters the pipeline
- **THEN** later behaviors can read the mapped headers, message id, correlation id, causation id, endpoint name, and resolved message type from the pipeline context

#### Scenario: Handler-facing context is created once
- **WHEN** the pipeline reaches handler or saga invocation
- **THEN** the handler-facing `MiniBusContext` is available from the pipeline context and preserves current metadata and outgoing operation behavior

#### Scenario: Outgoing operations are carried for persistence
- **WHEN** SQL outbox capture is enabled and handlers or sagas request outgoing operations
- **THEN** the pipeline context carries the captured operations to the persistence commit behavior

### Requirement: Pipeline supports explicit short-circuiting
MiniBus pipeline behaviors SHALL support intentional short-circuiting without treating the short-circuit as a processing failure.

#### Scenario: Duplicate inbox record short-circuits processing
- **WHEN** SQL inbox persistence is enabled and the incoming logical message id is already processed
- **THEN** the pipeline skips deserialization, handler invocation, saga invocation, outbox capture, and persistence commit for that message

#### Scenario: Short-circuited settlement-enabled message completes
- **WHEN** a settlement-enabled message is short-circuited as already processed
- **THEN** the settlement behavior completes the received Service Bus message

#### Scenario: Short-circuited no-settlement message returns
- **WHEN** a no-settlement message is short-circuited as already processed
- **THEN** the processor returns without invoking settlement APIs

### Requirement: Pipeline preserves failure flow
MiniBus pipeline behavior failures SHALL flow through the existing recoverability and propagation rules.

#### Scenario: No-settlement failure propagates
- **WHEN** no-settlement processing fails in a pipeline behavior
- **THEN** the original processing exception propagates to the caller

#### Scenario: Settlement-enabled failure uses recoverability
- **WHEN** settlement-enabled processing fails in a pipeline behavior
- **THEN** MiniBus evaluates the existing recoverability decision model and performs immediate retry, delayed retry, dead-letter, or propagation according to the decision

#### Scenario: Persistence commit failure is not converted to recoverability settlement
- **WHEN** the persistence commit behavior fails after handler success
- **THEN** MiniBus preserves the existing behavior where the commit failure prevents completion and propagates to the caller

### Requirement: Pipeline preserves handler and saga invocation semantics
MiniBus SHALL invoke regular handlers and sagas through pipeline behaviors without changing handler-facing contracts or saga lifecycle behavior.

#### Scenario: Regular handlers are invoked
- **WHEN** a received message is successfully deserialized and has matching handlers
- **THEN** the handler invocation behavior invokes them with the deserialized message, `MiniBusContext`, and cancellation token

#### Scenario: Saga processing is disabled
- **WHEN** saga processing is disabled in processor options
- **THEN** the saga behavior does not invoke saga handlers

#### Scenario: Saga processing is enabled
- **WHEN** saga processing is enabled and saga infrastructure is configured
- **THEN** the saga behavior invokes sagas with the same message and `MiniBusContext` semantics as before

### Requirement: Pipeline behavior is covered by focused unit tests
MiniBus SHALL cover the internal processing pipeline with focused unit tests in addition to existing end-to-end processor tests.

#### Scenario: Ordering and isolation are tested
- **WHEN** the pipeline test suite runs
- **THEN** it verifies behavior ordering and that each behavior owns a clear processing responsibility

#### Scenario: Short-circuiting and failure flow are tested
- **WHEN** the pipeline test suite runs
- **THEN** it verifies duplicate inbox short-circuiting, no-settlement failure propagation, settlement recoverability decisions, and persistence commit failure behavior

#### Scenario: Persistence, outbox, saga, and settlement behavior are tested
- **WHEN** the pipeline test suite runs
- **THEN** it verifies outbox capture, persistence commit, saga invocation, direct dispatch preservation, and settlement decisions

### Requirement: Pipeline exposes audit outcome information
MiniBus processing SHALL provide enough outcome information for audit writing after processing reaches a successful, skipped, delayed retry, dead-letter, or propagated terminal point.

#### Scenario: Successful outcome is available for auditing
- **WHEN** the pipeline processes a received message successfully
- **THEN** audit writing can observe the final successful outcome, message metadata, headers, resolved message type, handler context when created, and body context

#### Scenario: Duplicate outcome is available for auditing
- **WHEN** the pipeline short-circuits a received message because inbox persistence detected a duplicate
- **THEN** audit writing can observe the skipped duplicate outcome and the inbox message metadata used for duplicate detection

#### Scenario: Recoverability outcome is available for auditing
- **WHEN** recoverability selects delayed retry or dead-letter for a failed received message
- **THEN** audit writing can observe the recoverability decision, settlement decision, updated headers, and failure metadata available to MiniBus

### Requirement: Pipeline invokes audit without changing disabled behavior
MiniBus processing SHALL invoke audit writing only when an audit writer is configured.

#### Scenario: Audit writer absent
- **WHEN** no audit writer is configured
- **THEN** the processing pipeline preserves existing ordering, short-circuiting, failure propagation, persistence commit, recoverability, and settlement behavior

#### Scenario: Audit writer present
- **WHEN** an audit writer is configured and the message reaches an auditable outcome
- **THEN** the processing pipeline invokes the audit writer before returning success or applying final settlement for that outcome

### Requirement: Pipeline handles audit failures as processing failures
MiniBus processing SHALL not hide audit write failures when audit writing is enabled.

#### Scenario: Audit fails after handler success
- **WHEN** handler and saga processing succeeds but audit writing fails
- **THEN** the pipeline treats the audit failure as the processing failure for that invocation

#### Scenario: Audit fails for duplicate outcome
- **WHEN** duplicate inbox detection short-circuits processing but audit writing fails
- **THEN** the pipeline treats the audit failure as the processing failure for that invocation

#### Scenario: Audit fails for recoverability outcome
- **WHEN** delayed retry or dead-letter outcome audit writing fails
- **THEN** MiniBus does not settle the original message as if the audit write succeeded
