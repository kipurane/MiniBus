## ADDED Requirements

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
