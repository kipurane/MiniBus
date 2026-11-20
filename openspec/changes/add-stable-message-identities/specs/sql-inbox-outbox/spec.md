## ADDED Requirements

### Requirement: SQL outbox stores stable message identities
MiniBus SQL persistence SHALL store the selected outgoing message identity in the outbox `MessageType` field and preserved MiniBus headers without requiring a schema change.

#### Scenario: Registered contract is captured
- **WHEN** SQL-backed processing captures an outgoing operation for a contract with a registered stable identity
- **THEN** the outbox row stores the stable identity in its message type metadata and headers

#### Scenario: Unregistered contract is captured
- **WHEN** SQL-backed processing captures an outgoing operation for a contract without a stable identity registration
- **THEN** the outbox row stores the existing assembly-qualified CLR identity

### Requirement: SQL outbox resolves stable and legacy identities
MiniBus SQL persistence SHALL resolve stored outbox message identities through the configured registry before using the CLR fallback so both new and existing rows remain dispatchable.

#### Scenario: Stable outbox row is dispatched
- **WHEN** the dispatcher loads an outbox row containing a registered stable identity
- **THEN** it resolves the current CLR contract for routing and preserves the stored stable identity for transport dispatch

#### Scenario: Legacy outbox row is dispatched
- **WHEN** the dispatcher loads an existing outbox row containing a resolvable assembly-qualified CLR identity
- **THEN** it dispatches the row using the compatibility fallback without a data migration

#### Scenario: Historical CLR name is registered as an alias
- **WHEN** the dispatcher loads an outbox row whose historical CLR identity is an alias for a renamed current contract
- **THEN** it resolves the row to the current contract type and preserves the stored historical identity during dispatch

#### Scenario: Stored identity is unknown
- **WHEN** the dispatcher loads an outbox row whose message identity cannot be resolved
- **THEN** dispatch fails with a clear identity resolution error and existing outbox failure and retry metadata behavior applies
