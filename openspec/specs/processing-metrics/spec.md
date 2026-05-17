# processing-metrics Specification

## Purpose
Define provider-neutral MiniBus metrics emitted through `System.Diagnostics.Metrics` for processing duration, handler duration, recoverability outcomes, saga handling, and SQL outbox dispatch without requiring OpenTelemetry SDK dependencies.

## Requirements
### Requirement: Processing metrics use Meter instrumentation
MiniBus SHALL emit provider-neutral processing metrics through `System.Diagnostics.Metrics` without depending on the OpenTelemetry SDK, exporters, dashboards, or collector configuration.

#### Scenario: Host listens to MiniBus processing Meter
- **WHEN** an application enables metrics by listening to the documented MiniBus processing Meter name
- **THEN** MiniBus emits processing instruments that the application can export through its chosen metrics infrastructure

#### Scenario: Host has no metrics listener
- **WHEN** no metrics listener is attached to the MiniBus processing Meter
- **THEN** MiniBus processing continues without requiring metrics configuration and without performing expensive metric tag or duration work

#### Scenario: Metrics contracts are stable
- **WHEN** MiniBus defines Meter names, instrument names, units, descriptions, or tag names
- **THEN** those names are treated as stable observability contracts and documented for application operators

### Requirement: Processing attempt metrics are recorded
MiniBus SHALL record processing attempt count and duration metrics for each processing attempt with bounded processing metadata.

#### Scenario: Successful processing records duration
- **WHEN** a received message is processed successfully
- **THEN** MiniBus records a processing attempt count and processing duration tagged with endpoint name, message type, and the `completed` processing outcome

#### Scenario: Duplicate processing records outcome
- **WHEN** inbox persistence identifies a received message as already processed
- **THEN** MiniBus records processing attempt metrics tagged with the skipped-duplicate outcome and increments a duplicate processing count

#### Scenario: Failed processing records outcome
- **WHEN** processing fails and the exception is propagated
- **THEN** MiniBus records processing attempt metrics tagged with the failed outcome and increments a processing failure count

#### Scenario: Immediate retry records separate attempt
- **WHEN** a processing exception produces an immediate retry decision
- **THEN** MiniBus records the failed attempt's duration and increments retry metrics before the next immediate retry attempt starts

### Requirement: Recoverability metrics are recorded
MiniBus SHALL record bounded metrics for recoverability decisions including immediate retries, delayed retries, and dead-letter outcomes.

#### Scenario: Immediate retry increments retry count
- **WHEN** recoverability chooses an immediate retry
- **THEN** MiniBus increments a retry count tagged with endpoint name, message type, and an immediate retry kind

#### Scenario: Delayed retry increments retry count
- **WHEN** recoverability schedules a delayed retry successfully
- **THEN** MiniBus increments a retry count tagged with endpoint name, message type, and a delayed retry kind

#### Scenario: Dead-letter increments dead-letter count
- **WHEN** recoverability dead-letters a message
- **THEN** MiniBus increments a dead-letter count tagged with endpoint name and message type without tagging dead-letter description or exception message

### Requirement: Handler invocation metrics are recorded
MiniBus SHALL record duration metrics for each handler invocation without changing public handler APIs.

#### Scenario: Handler completes
- **WHEN** MiniBus invokes a message handler and the handler completes successfully
- **THEN** MiniBus records handler duration tagged with endpoint name, message type, handler type, and a completed handler outcome

#### Scenario: Handler fails
- **WHEN** MiniBus invokes a message handler and the handler throws or returns a faulted task
- **THEN** MiniBus records handler duration tagged with endpoint name, message type, handler type, and a failed handler outcome before normal recoverability handling continues

#### Scenario: Multiple handlers are measured independently
- **WHEN** a message has multiple registered handlers
- **THEN** MiniBus records a separate handler duration measurement for each invoked handler type

### Requirement: Saga metrics are recorded
MiniBus SHALL record saga handling duration and completion count metrics when saga processing is enabled and saga metadata is available.

#### Scenario: Saga handles message
- **WHEN** MiniBus invokes a saga for a received message
- **THEN** MiniBus records saga handling duration tagged with endpoint name, message type, saga type, and a bounded saga outcome

#### Scenario: Saga completes
- **WHEN** a saga invocation completes saga data
- **THEN** MiniBus increments a saga completion count tagged with endpoint name, message type, and saga type

#### Scenario: Saga metrics exclude correlation values
- **WHEN** MiniBus records saga metrics
- **THEN** MiniBus does not tag metrics with saga correlation id or message correlation id values

### Requirement: SQL outbox dispatch metrics are recorded
MiniBus SHALL record SQL outbox dispatch metrics for dispatcher batches and individual outbox operations.

#### Scenario: Dispatch batch completes
- **WHEN** `SqlMiniBusOutboxDispatcher.DispatchPendingAsync` claims and dispatches pending operations
- **THEN** MiniBus records batch duration and batch count metrics tagged with a bounded dispatch outcome

#### Scenario: Dispatch operation succeeds
- **WHEN** an outbox operation is dispatched and marked as dispatched
- **THEN** MiniBus records operation duration and operation count metrics tagged with operation kind and a succeeded dispatch outcome

#### Scenario: Dispatch operation fails
- **WHEN** an outbox operation dispatch fails and is marked failed for retry
- **THEN** MiniBus records operation duration and operation count metrics tagged with operation kind and a failed dispatch outcome

#### Scenario: Dispatch batch records operation counts
- **WHEN** a dispatch batch finishes
- **THEN** MiniBus records claimed, dispatched, and failed operation counts without tagging SQL row ids or outgoing transport message ids

### Requirement: Metric tags avoid high cardinality values
MiniBus SHALL use bounded metric tags and SHALL NOT tag metrics with message-specific, correlation-specific, or storage-row-specific identifiers.

#### Scenario: Processing metrics omit message identifiers
- **WHEN** MiniBus records processing, handler, recoverability, or saga metrics
- **THEN** metric tags exclude message id, correlation id, causation id, saga correlation id, and exception message

#### Scenario: Outbox metrics omit row identifiers
- **WHEN** MiniBus records SQL outbox dispatch metrics
- **THEN** metric tags exclude SQL row id, outgoing transport message id, exception message, and message body values

### Requirement: Metrics are test-verifiable
MiniBus SHALL provide test coverage or verification hooks that assert metrics behavior without requiring OpenTelemetry SDK packages.

#### Scenario: MeterListener tests verify processing metrics
- **WHEN** the MiniBus Azure Functions test suite verifies processing metrics
- **THEN** it uses `MeterListener` or equivalent BCL hooks to assert Meter name, instrument names, measurements, units, tags, and representative processing outcomes

#### Scenario: MeterListener tests verify outbox metrics
- **WHEN** the MiniBus SQL persistence test suite verifies outbox dispatch metrics
- **THEN** it uses `MeterListener` or equivalent BCL hooks to assert dispatch batch and operation metrics for successful and failed dispatches

#### Scenario: Metrics tests cover representative outcomes
- **WHEN** metrics tests run
- **THEN** they cover successful processing, immediate retry, delayed retry, dead-lettering, duplicate inbox skip, propagated failure, handler diagnostics, saga diagnostics, and outbox dispatch diagnostics where the current pipeline supports those outcomes

### Requirement: Metrics are documented
MiniBus SHALL document the metrics Meter names, instrument names, tag names, units, no-SDK behavior, and high-cardinality tag exclusions.

#### Scenario: Developers read observability documentation
- **WHEN** developers read MiniBus observability documentation
- **THEN** they can find the processing and SQL outbox Meter names, key instrument names, key tag names, duration units, and the statement that these names are stable observability contracts

#### Scenario: Documentation describes exporter responsibility
- **WHEN** developers read MiniBus metrics documentation
- **THEN** it states that MiniBus emits BCL metrics and applications remain responsible for choosing any OpenTelemetry SDK, exporter, collector, dashboard, or alerting configuration

