## ADDED Requirements

### Requirement: Audit writing is opt-in
MiniBus SHALL leave inbound processing behavior unchanged unless audit writing is explicitly configured.

#### Scenario: Audit writer is not configured
- **WHEN** a received message is processed without an audit writer registration
- **THEN** MiniBus processes, retries, dead-letters, completes, or propagates the message according to the existing processing behavior without attempting to write an audit record

#### Scenario: Audit writer is configured
- **WHEN** a received message reaches an auditable processing outcome
- **THEN** MiniBus invokes the configured audit writer with an audit record for that outcome

### Requirement: Audit records describe processed inbound messages
MiniBus SHALL create audit records that describe the received message, processing context, outcome, and available payload reference information without requiring handlers to use audit-specific APIs.

#### Scenario: Successful message is audited
- **WHEN** a received message is processed successfully
- **THEN** the audit record contains the endpoint name, message id, correlation id, causation id when available, message type when resolved, processing outcome, received headers, and audit timestamp

#### Scenario: Duplicate message is audited
- **WHEN** inbox persistence identifies a received message as already processed
- **THEN** the audit record identifies the outcome as a skipped duplicate and includes the logical message id used for duplicate detection

#### Scenario: Delayed retry outcome is audited
- **WHEN** recoverability schedules a delayed retry copy for a failed received message
- **THEN** the audit record identifies the outcome as delayed retry scheduled and includes retry metadata available in the MiniBus headers

#### Scenario: Dead-letter outcome is audited
- **WHEN** recoverability selects a dead-letter outcome for a failed received message
- **THEN** the audit record identifies the outcome as dead-lettered and includes the dead-letter reason and description when available

### Requirement: Audit records preserve body or claim-check context
MiniBus SHALL include payload context in audit records so an operator can understand whether the inbound body was inline or claim-checked.

#### Scenario: Inline message body is auditable
- **WHEN** MiniBus audits an inline received message
- **THEN** the audit record includes the inline body representation according to the configured audit body capture policy

#### Scenario: Claim-checked message is auditable
- **WHEN** MiniBus audits a claim-checked received message
- **THEN** the audit record includes MiniBus claim-check provider, container, blob name, payload id, length, content type, created timestamp, and expiry timestamp when those values are available

#### Scenario: Claim-checked body duplication is disabled
- **WHEN** MiniBus audits a claim-checked received message with claim-check body duplication disabled
- **THEN** the audit record includes claim-check metadata without duplicating the resolved large payload bytes into the audit record

### Requirement: Audit writer failures are explicit
MiniBus SHALL treat audit writer failures as processing failures when audit writing is enabled.

#### Scenario: Audit write fails before settlement
- **WHEN** settlement-enabled processing reaches an auditable outcome but writing the audit record fails
- **THEN** MiniBus does not complete or dead-letter the received Service Bus message as if auditing succeeded

#### Scenario: Audit write fails without settlement
- **WHEN** no-settlement processing reaches an auditable outcome but writing the audit record fails
- **THEN** MiniBus propagates the audit failure to the caller

### Requirement: Audit behavior is documented and tested
MiniBus SHALL document and test audit writer configuration, audit record content, outcome coverage, and failure behavior.

#### Scenario: Documentation shows audit setup
- **WHEN** a developer reads MiniBus Azure Storage persistence documentation
- **THEN** it shows how to enable Blob-backed audit writing, configure audit storage, and understand audit failure behavior

#### Scenario: Unit tests cover audit behavior
- **WHEN** the normal test suite runs
- **THEN** it verifies audit record construction, body and claim-check metadata selection, outcome mapping, and audit failure behavior without requiring live Azure resources
