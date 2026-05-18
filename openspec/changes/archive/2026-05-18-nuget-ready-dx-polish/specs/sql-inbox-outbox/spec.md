## ADDED Requirements

### Requirement: SQL inbox/outbox documentation references all schema scripts
MiniBus SQL inbox/outbox documentation SHALL instruct developers to apply all packaged SQL schema scripts in filename order before enabling SQL persistence.

#### Scenario: Developer reads SQL setup documentation
- **WHEN** a developer reads SQL inbox/outbox setup documentation
- **THEN** it points to `src/MiniBus.Persistence.Sql/Schema/` and instructs the developer to apply all scripts in filename order

#### Scenario: Additional schema scripts exist
- **WHEN** the SQL persistence package contains more than one schema script
- **THEN** setup documentation does not imply that applying only the first inbox/outbox script is sufficient

#### Scenario: Developer uses custom SQL names
- **WHEN** a developer configures custom SQL schema or table names
- **THEN** setup documentation explains that packaged scripts target default names unless the application adapts them

### Requirement: SQL outbox documentation explains dispatch and drain behavior
MiniBus SQL inbox/outbox documentation SHALL describe how persisted outbox operations are dispatched or drained after successful processing.

#### Scenario: Developer enables SQL outbox
- **WHEN** a developer reads SQL outbox documentation
- **THEN** it explains that outgoing operations are captured durably before completion and later dispatched through `SqlMiniBusOutboxDispatcher`

#### Scenario: Developer evaluates retry semantics
- **WHEN** a developer reads SQL outbox documentation
- **THEN** it explains at-least-once dispatch, deterministic outgoing message ids, claim lease recovery, and downstream idempotency expectations
