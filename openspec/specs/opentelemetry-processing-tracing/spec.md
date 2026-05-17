# opentelemetry-processing-tracing Specification

## Purpose
Define MiniBus processing trace contracts emitted through provider-neutral `System.Diagnostics.ActivitySource` instrumentation.

## Requirements
### Requirement: Processing tracing uses ActivitySource
MiniBus SHALL emit provider-neutral processing traces through `System.Diagnostics.ActivitySource` without depending on the OpenTelemetry SDK, exporters, dashboards, or collector configuration.

#### Scenario: Host listens to MiniBus ActivitySource
- **WHEN** an application enables tracing by listening to the documented MiniBus ActivitySource name
- **THEN** MiniBus emits processing activities that the application can export through its chosen tracing infrastructure

#### Scenario: No listener is attached
- **WHEN** no Activity listener is attached to the MiniBus ActivitySource
- **THEN** MiniBus processing continues without requiring tracing configuration and without creating sampled processing activities

### Requirement: Processing attempts create root activities
MiniBus SHALL create one root processing activity for each received message processing attempt after received message metadata is available.

#### Scenario: Processing attempt starts
- **WHEN** MiniBus starts processing a received Service Bus message
- **THEN** it starts a processing activity named `MiniBus.Process` with the documented ActivitySource name

#### Scenario: Immediate retry starts a new attempt
- **WHEN** recoverability selects an immediate retry and MiniBus starts the retry attempt
- **THEN** MiniBus creates a separate `MiniBus.Process` activity for the retry attempt with updated retry metadata

#### Scenario: Processing attempt ends
- **WHEN** a processing attempt reaches a terminal outcome
- **THEN** MiniBus stops the processing activity after outcome, error, and relevant diagnostic tags or events have been recorded

### Requirement: Processing activities include messaging and MiniBus tags
MiniBus SHALL attach stable activity tags for Azure messaging metadata and MiniBus processing metadata when those values are available.

#### Scenario: Messaging tags are available
- **WHEN** MiniBus creates a processing activity for an Azure Service Bus message
- **THEN** the activity includes `messaging.system = azure_service_bus` and includes destination tags such as `messaging.destination.name` only when MiniBus can determine the destination without guessing

#### Scenario: Core MiniBus tags are available
- **WHEN** MiniBus creates or updates a processing activity
- **THEN** the activity includes MiniBus tags for endpoint name, message type, message id, correlation id, causation id, retry attempt, and delayed retry attempt when available

#### Scenario: Invocation and outcome tags are available
- **WHEN** MiniBus learns handler, saga, outbox, dead-letter, or outcome metadata during processing
- **THEN** the activity includes MiniBus tags for handler type, saga type, saga correlation id, processing outcome, outbox operation count, and dead-letter reason when available

### Requirement: Processing activities record outcomes and errors
MiniBus SHALL record terminal processing outcomes and error status on processing activities.

#### Scenario: Processing completes successfully
- **WHEN** a received message is processed successfully and is not short-circuited as a duplicate
- **THEN** the activity records a completed processing outcome without setting error status

#### Scenario: Duplicate inbox message is skipped
- **WHEN** SQL inbox persistence detects that the received logical message id was already processed
- **THEN** the activity records a skipped-duplicate processing outcome without setting error status

#### Scenario: Immediate retry is selected
- **WHEN** processing fails and recoverability selects an immediate retry
- **THEN** the failed attempt activity records a retried processing outcome and retry metadata

#### Scenario: Delayed retry is scheduled
- **WHEN** processing fails and recoverability schedules a delayed retry successfully
- **THEN** the activity records a delayed-retry-scheduled processing outcome and delayed retry metadata without treating scheduling success as a trace instrumentation failure

#### Scenario: Message is dead-lettered
- **WHEN** processing fails and recoverability selects dead-lettering
- **THEN** the activity records a dead-lettered processing outcome, dead-letter metadata, and error status

#### Scenario: Failure propagates
- **WHEN** processing fails and MiniBus propagates the exception instead of settling the received message
- **THEN** the activity records failure metadata, exception details, and error status

#### Scenario: Infrastructure failure prevents settlement outcome
- **WHEN** persistence, audit writing, or delayed retry scheduling fails before MiniBus can complete the intended outcome
- **THEN** the activity records exception details and error status for the infrastructure failure

### Requirement: Processing activities record useful milestones
MiniBus SHALL record useful processing milestones as activity events or child activities without requiring a broad span model in the first tracing feature.

#### Scenario: Handler invocation milestone is recorded
- **WHEN** MiniBus invokes a message handler and a processing activity is active
- **THEN** MiniBus records handler invocation metadata on the activity as an event or child activity, including handler type when available

#### Scenario: Saga milestone is recorded
- **WHEN** MiniBus invokes or completes a saga and a processing activity is active
- **THEN** MiniBus records saga metadata on the activity as an event or child activity, including saga type and saga correlation id when available

#### Scenario: Outbox milestone is recorded
- **WHEN** MiniBus commits or dispatches outbox work and a processing activity is active
- **THEN** MiniBus records outbox metadata on the activity as an event or child activity, including operation count when available

#### Scenario: Recoverability milestone is recorded
- **WHEN** recoverability selects retry, delayed retry, dead-letter, or propagation
- **THEN** MiniBus records the recoverability decision on the active activity as tags, events, or status metadata

### Requirement: Tracing contracts are documented and test-verifiable
MiniBus SHALL document and test the tracing source, activity names, tag names, status behavior, and representative processing outcomes.

#### Scenario: Activity contracts are documented
- **WHEN** developers read MiniBus observability documentation
- **THEN** they can find the ActivitySource name, root activity name, key MiniBus tag names, and the statement that these names are stable observability contracts

#### Scenario: ActivityListener tests verify traces
- **WHEN** the MiniBus Azure Functions test suite verifies processing tracing
- **THEN** it uses `ActivityListener` or equivalent hooks to assert activity source name, activity name, tags, status, and representative events or child activities without requiring OpenTelemetry SDK packages

#### Scenario: Representative outcomes are tested
- **WHEN** tracing tests run
- **THEN** they cover successful processing, immediate retry, delayed retry, dead-lettering, duplicate inbox skip, propagated failure, handler diagnostics, saga diagnostics, and outbox diagnostics where the current pipeline supports those outcomes
