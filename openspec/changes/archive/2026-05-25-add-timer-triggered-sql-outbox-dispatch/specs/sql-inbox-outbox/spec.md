## MODIFIED Requirements

### Requirement: SQL persistence behavior is documented and tested
MiniBus SHALL document SQL inbox/outbox setup and cover SQL persistence behavior with automated tests, including manual dispatch, optional hosted outbox dispatch, timer-triggered Azure Functions dispatch guidance, Testcontainers-backed SQL Server integration tests, and external-connection-string SQL Server/Azure SQL integration tests.

#### Scenario: Documentation shows setup
- **WHEN** a developer reads the SQL persistence documentation
- **THEN** it shows registration, schema setup, processing behavior, manual dispatcher usage, optional hosted dispatcher usage, timer-triggered Azure Functions dispatcher guidance, and clarifies that outbox dispatch remains separate from handler execution

#### Scenario: Tests cover core persistence behavior
- **WHEN** the normal test suite runs
- **THEN** it verifies duplicate detection, outbox capture, transactional commit, manual dispatch success, hosted dispatch behavior, timer-triggered drain composition where applicable, dispatch failure retry metadata, and opt-in behavior without requiring live SQL Server infrastructure

#### Scenario: Integration tests cover SQL Server behavior
- **WHEN** SQL Server-backed integration tests run through Testcontainers or a configured test connection string
- **THEN** they verify schema creation, inbox duplicate detection, outbox capture, outbox claim and dispatch state, failure retry metadata, persistence transaction behavior, and hosted-dispatch or timer-triggered-dispatch compatibility against SQL Server-compatible storage

### Requirement: SQL outbox dispatch supports application-owned scheduling models
MiniBus SQL persistence SHALL support application-owned scheduling of outbox draining through the existing SQL outbox dispatcher so applications can choose manual commands, timer-triggered Functions, hosted services, or separate worker processes without changing durable outbox semantics.

#### Scenario: Timer-triggered Function drains outbox work
- **WHEN** an Azure Functions isolated worker application schedules outbox draining through a timer trigger
- **THEN** the function can resolve `SqlMiniBusOutboxDispatcher` and execute a bounded drain through existing dispatcher APIs
- **AND** MiniBus preserves the same SQL claim, dispatch, failure metadata, and claim-lease recovery behavior used by manual and hosted-service drains

#### Scenario: Timer-triggered dispatch remains at-least-once
- **WHEN** timer-triggered outbox dispatch is used
- **THEN** MiniBus continues to treat transport dispatch as at-least-once
- **AND** applications remain responsible for deterministic outgoing message ids, broker duplicate detection where available, and idempotent receivers

#### Scenario: Timer-triggered and hosted-service dispatch are distinct choices
- **WHEN** a developer compares SQL outbox dispatch hosting options
- **THEN** MiniBus documentation distinguishes timer-triggered Azure Functions dispatch from `IHostedService` dispatch
- **AND** it explains that both options schedule the same SQL outbox dispatcher rather than changing handler execution or SQL commit semantics
- **AND** it explains that same-process hosted dispatch can use the built-in best-effort wake-up after MiniBus-owned commits, while a separate dispatcher host discovers work through timer or polling cadence unless the application adds custom cross-process signaling
