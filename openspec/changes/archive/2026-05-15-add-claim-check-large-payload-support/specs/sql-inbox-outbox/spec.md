## ADDED Requirements

### Requirement: SQL outbox captures claim-checked outgoing operations
MiniBus SQL persistence SHALL capture outgoing operations after claim-check transformation so large payload bodies are stored externally and outbox rows contain compact transport-ready bodies and metadata.

#### Scenario: Claim-checked send is captured
- **WHEN** a handler sends an above-threshold command while SQL outbox persistence and claim-check behavior are enabled
- **THEN** the SQL outbox stores a send operation containing the compact claim-check body and MiniBus claim-check headers

#### Scenario: Claim-checked publish is captured
- **WHEN** a handler publishes an above-threshold event while SQL outbox persistence and claim-check behavior are enabled
- **THEN** the SQL outbox stores a publish operation containing the compact claim-check body and MiniBus claim-check headers

#### Scenario: Claim-checked schedule is captured
- **WHEN** a handler schedules an above-threshold message while SQL outbox persistence and claim-check behavior are enabled
- **THEN** the SQL outbox stores a scheduled operation containing the compact claim-check body, MiniBus claim-check headers, and persisted due time

### Requirement: SQL outbox replays claim-checked operations safely
MiniBus SQL persistence SHALL replay claim-checked outbox operations through the configured transport without requiring access to the original serialized body in SQL.

#### Scenario: Claim-checked operation is replayed
- **WHEN** the SQL outbox dispatcher claims and dispatches a claim-checked operation
- **THEN** it passes the stored compact body, stored MiniBus claim-check headers, and deterministic outgoing message id to the configured transport

#### Scenario: Claim-check metadata survives replay
- **WHEN** the same claim-checked outbox row is dispatched more than once because a previous attempt failed or crashed before marking the row as dispatched
- **THEN** each dispatch attempt uses the same stored claim-check metadata and deterministic outgoing message id

#### Scenario: Claim-checked dispatch failure records retry metadata
- **WHEN** dispatching a claim-checked outbox operation fails
- **THEN** MiniBus records outbox failure metadata using the existing outbox retry behavior
