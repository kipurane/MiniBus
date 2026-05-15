## ADDED Requirements

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
