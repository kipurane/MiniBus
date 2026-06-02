## MODIFIED Requirements

### Requirement: Inbox and outbox commit atomically
MiniBus SQL persistence SHALL commit inbox state, outbox operations, and any saga state changes produced during a successfully handled incoming message in one SQL transaction, and hosted outbox dispatch SHALL not weaken that commit-first boundary.

#### Scenario: Handler succeeds without saga changes
- **WHEN** handlers complete successfully during SQL-backed processing without saga state changes
- **THEN** MiniBus commits the inbox processed record and all captured outbox operations together

#### Scenario: Saga handler succeeds
- **WHEN** handlers and sagas complete successfully during SQL-backed processing
- **THEN** MiniBus commits the inbox processed record, all captured outbox operations, and all saga state changes together

#### Scenario: Handler succeeds with hosted dispatch enabled
- **WHEN** handlers complete successfully during SQL-backed processing and hosted outbox dispatch is enabled
- **THEN** MiniBus commits the inbox processed record, all captured outbox operations, and any saga state changes before the incoming message is treated as complete
- **AND** transport dispatch happens only after that durable commit in a separate at-least-once phase

#### Scenario: Handler fails
- **WHEN** a handler throws during SQL-backed processing
- **THEN** MiniBus does not commit an inbox processed record, captured outbox operations, or saga state changes for that failed attempt

#### Scenario: Saga handler fails
- **WHEN** a saga handler throws during SQL-backed processing
- **THEN** MiniBus does not commit an inbox processed record, captured outbox operations, or saga state changes for that failed attempt

#### Scenario: Outbox insertion fails
- **WHEN** SQL outbox insertion fails after saga handling succeeds
- **THEN** MiniBus rolls back the SQL transaction
- **AND** no inbox record, outbox operation, or saga state change from that attempt remains durable

#### Scenario: Commit fails
- **WHEN** the SQL transaction cannot be committed after handler and saga success
- **THEN** MiniBus reports processing failure and does not treat the incoming message as complete
- **AND** no inbox record, outbox operation, or saga state change from that attempt remains durable
