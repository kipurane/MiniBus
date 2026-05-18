## ADDED Requirements

### Requirement: SQL saga persistence documentation is part of SQL setup
MiniBus SQL persistence documentation SHALL describe SQL saga persistence as part of the current SQL package capability.

#### Scenario: Developer reads SQL persistence documentation
- **WHEN** a developer reads SQL persistence documentation
- **THEN** it explains that SQL persistence can provide SQL-backed `ISagaPersistence` in addition to inbox and outbox behavior

#### Scenario: Developer prepares database schema
- **WHEN** a developer prepares a database for SQL saga persistence
- **THEN** documentation directs them to apply the packaged SQL schema scripts in filename order, including the saga schema script

#### Scenario: Developer configures saga processing
- **WHEN** a developer reads SQL saga setup documentation
- **THEN** it describes saga registration, SQL saga persistence registration, serialization expectations, completion behavior, and optimistic concurrency behavior
