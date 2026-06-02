## ADDED Requirements

### Requirement: SQL saga persistence participates in processing sessions
MiniBus SQL saga persistence SHALL use the active SQL processing session for saga load/create/save/complete operations during SQL-backed message processing so saga state shares the same SQL transaction as inbox and outbox state.

#### Scenario: Saga state is created in active transaction
- **WHEN** a starting saga message is processed with SQL persistence enabled
- **THEN** MiniBus creates the SQL saga record using the active processing connection and transaction
- **AND** the saga record is not visible as committed state unless the processing transaction commits

#### Scenario: Saga state is saved in active transaction
- **WHEN** a continuing saga message updates existing saga state with SQL persistence enabled
- **THEN** MiniBus saves the SQL saga record using the active processing connection and transaction
- **AND** the saga update is not visible as committed state unless the processing transaction commits

#### Scenario: Saga state is completed in active transaction
- **WHEN** a saga marks itself complete during SQL-backed processing
- **THEN** MiniBus completes the SQL saga record using the active processing connection and transaction
- **AND** the completion is not visible as committed state unless the processing transaction commits

#### Scenario: Saga timeout schedules outbox operation atomically
- **WHEN** a saga schedules a timeout message during SQL-backed processing
- **THEN** MiniBus commits the saga state change and the scheduled outbox operation in the same SQL transaction

#### Scenario: Saga concurrency conflict aborts processing
- **WHEN** SQL saga persistence detects a duplicate create, missing saga, or stale saga version during SQL-backed processing
- **THEN** MiniBus fails the processing attempt through normal recoverability behavior
- **AND** the active SQL transaction does not commit inbox, outbox, or saga state from that failed attempt

#### Scenario: Standalone SQL saga persistence remains explicit
- **WHEN** code uses SQL saga persistence outside a MiniBus processing session
- **THEN** MiniBus preserves standalone SQL saga load/create/save/complete behavior
- **AND** documentation distinguishes standalone saga persistence from the atomic SQL message-processing path
