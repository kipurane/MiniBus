## ADDED Requirements

### Requirement: Hosted outbox dispatch is opt-in
MiniBus SQL persistence SHALL keep automatic background outbox dispatch disabled unless an application explicitly enables hosted dispatch registration, and SHALL preserve manual dispatcher access regardless of that choice.

#### Scenario: Application does not enable hosted dispatch
- **WHEN** an application registers SQL persistence without opting into hosted outbox dispatch
- **THEN** MiniBus does not start a background outbox dispatch loop
- **AND** the application can continue to dispatch pending SQL outbox work manually or from a separate process

#### Scenario: Application enables hosted dispatch
- **WHEN** an application registers SQL persistence and opts into hosted outbox dispatch
- **THEN** MiniBus starts a background outbox dispatch service that uses the existing SQL outbox dispatcher behavior
- **AND** `SqlMiniBusOutboxDispatcher` remains available for manual, test, or external dispatcher use

### Requirement: Hosted outbox dispatch lifecycle is configurable and recoverable
MiniBus SQL persistence SHALL provide hosted outbox dispatch configuration for polling cadence, bounded batch execution, startup behavior, failure backoff, graceful shutdown, and best-effort in-process wake-up while relying on polling and claim-lease recovery for correctness.

#### Scenario: Polling and bounded drain are honored
- **WHEN** hosted outbox dispatch runs with a configured polling interval and maximum batches per cycle
- **THEN** MiniBus waits according to the configured polling behavior when no wake-up is pending
- **AND** each dispatch cycle stops after the configured maximum number of batches or earlier when no pending work is dispatched

#### Scenario: Failure backoff is applied
- **WHEN** a hosted dispatch cycle fails before the service returns to its idle state
- **THEN** MiniBus delays the next hosted dispatch attempt according to the configured backoff
- **AND** pending work remains eligible for later dispatch through the normal claim and retry behavior

#### Scenario: Startup drain is optional
- **WHEN** hosted outbox dispatch is configured to drain on startup
- **THEN** MiniBus starts an initial dispatch cycle without waiting for the normal idle polling interval

#### Scenario: In-process wake-up is best-effort
- **WHEN** hosted outbox dispatch is enabled and MiniBus commits new outbox work in a MiniBus-owned transaction
- **THEN** MiniBus can request an earlier in-process dispatch cycle for the local host
- **AND** correctness does not depend on that signal because polling and claim-lease recovery still discover pending work

#### Scenario: Shutdown preserves recoverability
- **WHEN** the host begins shutting down while hosted outbox dispatch is enabled
- **THEN** MiniBus stops starting new dispatch cycles and cancels in-flight work through normal host cancellation
- **AND** any rows not fully dispatched remain recoverable through the existing outbox claim-lease behavior

## MODIFIED Requirements

### Requirement: Inbox and outbox commit atomically
MiniBus SQL persistence SHALL commit inbox state and outbox operations for a successfully handled incoming message in one SQL transaction, and hosted outbox dispatch SHALL not weaken that commit-first boundary.

#### Scenario: Handler succeeds
- **WHEN** handlers complete successfully during SQL-backed processing
- **THEN** MiniBus commits the inbox processed record and all captured outbox operations together

#### Scenario: Handler succeeds with hosted dispatch enabled
- **WHEN** handlers complete successfully during SQL-backed processing and hosted outbox dispatch is enabled
- **THEN** MiniBus commits the inbox processed record and all captured outbox operations before the incoming message is treated as complete
- **AND** transport dispatch happens only after that durable commit in a separate at-least-once phase

#### Scenario: Handler fails
- **WHEN** a handler throws during SQL-backed processing
- **THEN** MiniBus does not commit an inbox processed record or captured outbox operations for that failed attempt

#### Scenario: Commit fails
- **WHEN** the SQL transaction cannot be committed after handler success
- **THEN** MiniBus reports processing failure and does not treat the incoming message as complete

### Requirement: SQL persistence behavior is documented and tested
MiniBus SHALL document SQL inbox/outbox setup and cover SQL persistence behavior with automated tests, including manual dispatch, optional hosted outbox dispatch, Testcontainers-backed SQL Server integration tests, and external-connection-string SQL Server/Azure SQL integration tests.

#### Scenario: Documentation shows setup
- **WHEN** a developer reads the SQL persistence documentation
- **THEN** it shows registration, schema setup, processing behavior, manual dispatcher usage, optional hosted dispatcher usage, and clarifies that outbox dispatch remains separate from handler execution

#### Scenario: Tests cover core persistence behavior
- **WHEN** the normal test suite runs
- **THEN** it verifies duplicate detection, outbox capture, transactional commit, manual dispatch success, hosted dispatch behavior, dispatch failure retry metadata, and opt-in behavior without requiring live SQL Server infrastructure

#### Scenario: Integration tests cover SQL Server behavior
- **WHEN** SQL Server-backed integration tests run through Testcontainers or a configured test connection string
- **THEN** they verify schema creation, inbox duplicate detection, outbox capture, outbox claim and dispatch state, failure retry metadata, persistence transaction behavior, and hosted-dispatch compatibility against SQL Server-compatible storage