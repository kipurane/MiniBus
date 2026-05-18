# sql-inbox-outbox Specification

## Purpose
Defines SQL Server / Azure SQL inbox and outbox persistence for MiniBus, including processed-message deduplication, durable outgoing operation capture, transactional commit, and outbox dispatch.
## Requirements
### Requirement: SQL persistence package isolates storage dependencies
MiniBus SHALL provide a SQL persistence package for SQL Server / Azure SQL inbox and outbox behavior without requiring application handlers to reference SQL client APIs.

#### Scenario: Handlers remain storage independent
- **WHEN** an application enables SQL inbox/outbox persistence
- **THEN** handlers continue to depend only on MiniBus message contracts and `MiniBusContext`

### Requirement: SQL schema stores processed incoming messages
MiniBus SQL persistence SHALL define schema objects for recording processed incoming messages by endpoint name and logical message id.

#### Scenario: Processed message is recorded
- **WHEN** a message is successfully processed with SQL inbox enabled
- **THEN** MiniBus records the endpoint name, logical message id, processing timestamp, and relevant correlation metadata in the inbox store

#### Scenario: Duplicate processed message is detected
- **WHEN** a message arrives for an endpoint and the inbox store already contains its logical message id
- **THEN** MiniBus treats the message as already processed and does not invoke its handlers again

### Requirement: Inbox identity preserves retry semantics
MiniBus SQL persistence SHALL use `MiniBus.OriginalMessageId` as the logical inbox message id when present and SHALL fall back to the received message id when it is absent.

#### Scenario: Delayed retry copy uses original identity
- **WHEN** a delayed retry copy contains `MiniBus.OriginalMessageId`
- **THEN** the inbox lookup uses that original id instead of the retry copy transport id

#### Scenario: First delivery uses received identity
- **WHEN** a message does not contain `MiniBus.OriginalMessageId`
- **THEN** the inbox lookup uses the received message id

### Requirement: SQL outbox stores outgoing operations
MiniBus SQL persistence SHALL store outgoing `Send`, `Publish`, and `Schedule` operations requested during message handling before those operations are dispatched.

#### Scenario: Handler sends command with SQL outbox enabled
- **WHEN** a handler calls `MiniBusContext.Send` during SQL-backed processing
- **THEN** MiniBus stores a pending outbox operation representing the command

#### Scenario: Handler publishes event with SQL outbox enabled
- **WHEN** a handler calls `MiniBusContext.Publish` during SQL-backed processing
- **THEN** MiniBus stores a pending outbox operation representing the event

#### Scenario: Handler schedules message with SQL outbox enabled
- **WHEN** a handler calls `MiniBusContext.Schedule` during SQL-backed processing
- **THEN** MiniBus stores a pending outbox operation with the requested due time

### Requirement: Outbox records preserve message metadata
MiniBus SQL persistence SHALL persist enough metadata for each outgoing operation to reconstruct and dispatch the operation through the configured MiniBus transport.

#### Scenario: Outgoing metadata is persisted
- **WHEN** an outgoing operation is stored in the SQL outbox
- **THEN** the record includes operation kind, message type metadata, serialized body, headers, correlation metadata, created timestamp, and due time when applicable

#### Scenario: Causation metadata is preserved
- **WHEN** an outgoing operation is created while handling an incoming message
- **THEN** the stored headers identify the incoming message as the causation id

### Requirement: Inbox and outbox commit atomically
MiniBus SQL persistence SHALL commit inbox state and outbox operations for a successfully handled incoming message in one SQL transaction.

#### Scenario: Handler succeeds
- **WHEN** handlers complete successfully during SQL-backed processing
- **THEN** MiniBus commits the inbox processed record and all captured outbox operations together

#### Scenario: Handler fails
- **WHEN** a handler throws during SQL-backed processing
- **THEN** MiniBus does not commit an inbox processed record or captured outbox operations for that failed attempt

#### Scenario: Commit fails
- **WHEN** the SQL transaction cannot be committed after handler success
- **THEN** MiniBus reports processing failure and does not treat the incoming message as complete

### Requirement: Outbox dispatcher claims pending operations
MiniBus SQL persistence SHALL provide an outbox dispatcher that claims a bounded batch of pending operations before dispatching them.

#### Scenario: Pending operation is claimed
- **WHEN** the outbox dispatcher selects pending work
- **THEN** it marks selected rows as claimed so concurrent dispatcher instances do not intentionally dispatch the same rows

#### Scenario: Batch size is honored
- **WHEN** the dispatcher is configured with a maximum batch size
- **THEN** it claims no more than that number of pending operations in one dispatch cycle

### Requirement: Outbox dispatcher sends through configured transport
MiniBus SQL persistence SHALL dispatch claimed outbox operations through the configured MiniBus transport dispatch abstraction.

#### Scenario: Send operation is dispatched
- **WHEN** the dispatcher processes a claimed send operation
- **THEN** it dispatches the command through the configured transport

#### Scenario: Publish operation is dispatched
- **WHEN** the dispatcher processes a claimed publish operation
- **THEN** it dispatches the event through the configured transport

#### Scenario: Schedule operation is dispatched
- **WHEN** the dispatcher processes a claimed schedule operation
- **THEN** it dispatches the message with the stored due time through the configured transport

### Requirement: Outbox dispatch state is durable
MiniBus SQL persistence SHALL durably record dispatch success and failure metadata for outbox operations.

#### Scenario: Dispatch succeeds
- **WHEN** an outbox operation is dispatched successfully
- **THEN** MiniBus marks the operation as dispatched with a dispatched timestamp

#### Scenario: Dispatch fails
- **WHEN** an outbox operation dispatch attempt fails
- **THEN** MiniBus records attempt metadata and last error details while keeping the operation eligible for retry

### Requirement: SQL Server provider registration is first-class
MiniBus SQL persistence SHALL provide first-class SQL Server / Azure SQL registration backed by `Microsoft.Data.SqlClient` while preserving caller-provided `DbConnection` factory registration.

#### Scenario: Application registers SQL persistence with a connection string
- **WHEN** an application registers MiniBus SQL persistence with a SQL Server / Azure SQL connection string
- **THEN** MiniBus configures SQL inbox, SQL outbox, and outbox dispatcher services that create `SqlConnection` instances for persistence operations

#### Scenario: Application registers SQL persistence with a connection factory
- **WHEN** an application registers MiniBus SQL persistence with a caller-provided `DbConnection` factory
- **THEN** MiniBus uses that factory for SQL inbox, SQL outbox, and outbox dispatcher services

#### Scenario: Existing factory registration remains supported
- **WHEN** an application uses the existing `DbConnection` factory setup path
- **THEN** MiniBus preserves the existing provider-neutral behavior without requiring the application to use connection-string registration

### Requirement: SQL persistence is configured through dependency injection
MiniBus SQL persistence SHALL provide dependency injection registration for SQL Server / Azure SQL connection strings, caller-provided connection factories, schema options, inbox services, outbox services, and dispatcher services.

#### Scenario: Application enables SQL persistence with a connection string
- **WHEN** an application registers MiniBus SQL persistence with a SQL Server / Azure SQL connection string
- **THEN** MiniBus can resolve the SQL inbox, SQL outbox, and outbox dispatcher services using SqlClient-backed connections

#### Scenario: Application enables SQL persistence with a connection factory
- **WHEN** an application registers MiniBus SQL persistence with a caller-provided `DbConnection` factory
- **THEN** MiniBus can resolve the SQL inbox, SQL outbox, and outbox dispatcher services using the provided factory

#### Scenario: SQL persistence is not enabled
- **WHEN** an application does not register SQL persistence
- **THEN** MiniBus continues to use its existing non-SQL processing and direct-dispatch behavior

### Requirement: SQL Server integration tests are opt-in
MiniBus SHALL provide SQL Server-compatible integration tests for SQL persistence behavior that can run against either a Testcontainers-managed SQL Server container or a configured SQL Server / Azure SQL test connection string.

#### Scenario: Docker is available and no external test connection string is configured
- **WHEN** the SQL persistence integration test suite runs with Docker available and no documented SQL Server / Azure SQL test connection string
- **THEN** the suite provisions a SQL Server-compatible test database using Testcontainers and verifies schema creation, inbox duplicate detection, outbox capture, outbox claim and dispatch state, failure retry metadata, and persistence transaction behavior

#### Scenario: External test connection string is configured
- **WHEN** the SQL persistence integration test suite runs with a documented SQL Server / Azure SQL test connection string
- **THEN** the suite uses the configured database instead of starting a container and verifies schema creation, inbox duplicate detection, outbox capture, outbox claim and dispatch state, failure retry metadata, and persistence transaction behavior

#### Scenario: Apple Silicon host runs container-backed tests
- **WHEN** the SQL persistence integration test suite runs on an Apple Silicon host using the container-backed path
- **THEN** the suite uses a SQL Server container image and platform strategy compatible with Docker Desktop on Apple Silicon, or skips with a clear message when that strategy is unavailable

#### Scenario: Neither Docker nor external test connection string is available
- **WHEN** the SQL persistence integration test suite runs without Docker availability and without the documented SQL Server / Azure SQL test connection string
- **THEN** SQL Server-backed integration tests are skipped with a clear reason without failing the normal test run

### Requirement: SQL Server setup is documented
MiniBus SHALL document the SQL Server / Azure SQL setup path and the Testcontainers-backed verification path for SQL inbox/outbox persistence.

#### Scenario: Developer reads SQL persistence setup documentation
- **WHEN** a developer reads MiniBus SQL persistence documentation
- **THEN** it shows connection-string registration, schema script application, optional connection-factory registration, outbox dispatcher usage, Testcontainers-backed integration test execution, Apple Silicon Docker requirements, and the external connection string fallback

### Requirement: SQL persistence behavior is documented and tested
MiniBus SHALL document SQL inbox/outbox setup and cover SQL persistence behavior with automated tests, including Testcontainers-backed SQL Server integration tests and external-connection-string SQL Server/Azure SQL integration tests.

#### Scenario: Documentation shows setup
- **WHEN** a developer reads the SQL persistence documentation
- **THEN** it shows registration, schema setup, processing behavior, outbox dispatcher usage, and SQL Server/Azure SQL connection-string setup

#### Scenario: Tests cover core persistence behavior
- **WHEN** the normal test suite runs
- **THEN** it verifies duplicate detection, outbox capture, transactional commit, dispatch success, dispatch failure retry metadata, and opt-in behavior without requiring live SQL Server infrastructure

#### Scenario: Integration tests cover SQL Server behavior
- **WHEN** SQL Server-backed integration tests run through Testcontainers or a configured test connection string
- **THEN** they verify schema creation, inbox duplicate detection, outbox capture, outbox claim and dispatch state, failure retry metadata, and persistence transaction behavior against SQL Server-compatible storage

### Requirement: SQL persistence supports application-owned transactions
MiniBus SQL persistence SHALL support committing inbox and outbox changes inside an application-owned SQL transaction when the application explicitly provides an open connection and active transaction.

#### Scenario: Application-owned transaction commits business data and MiniBus state
- **WHEN** an application handles a message, writes business data, and asks MiniBus SQL persistence to commit inbox and outbox state using the same SQL transaction
- **THEN** the business data, inbox record, and outbox records are committed or rolled back as one database transaction by the application owner

#### Scenario: MiniBus does not complete application-owned transaction
- **WHEN** MiniBus SQL persistence writes inbox and outbox state using an application-owned transaction
- **THEN** MiniBus does not commit, roll back, or dispose the application-owned transaction

#### Scenario: Invalid application-owned transaction is rejected
- **WHEN** an application provides a closed connection, missing transaction, or transaction that does not belong to the provided connection
- **THEN** MiniBus rejects the commit before writing inbox or outbox records

### Requirement: SQL persistence keeps MiniBus-owned transaction behavior
MiniBus SQL persistence SHALL continue to provide a MiniBus-owned transaction path for applications that do not share a transaction with business data.

#### Scenario: MiniBus-owned transaction commits persistence state
- **WHEN** handlers complete successfully during SQL-backed processing without an application-owned transaction
- **THEN** MiniBus opens a SQL connection, begins a SQL transaction, commits the inbox record and outbox records together, and owns transaction completion

#### Scenario: MiniBus-owned transaction rolls back on failure
- **WHEN** MiniBus cannot write or commit SQL persistence state in the MiniBus-owned transaction path
- **THEN** MiniBus rolls back its transaction and reports processing failure

### Requirement: Outbox operations have deterministic outgoing message ids
MiniBus SQL persistence SHALL store a deterministic outgoing message id for each persisted outbox operation and SHALL reuse that id on every dispatch attempt for the same operation.

#### Scenario: Outbox operation is captured
- **WHEN** MiniBus stores an outgoing operation in the SQL outbox
- **THEN** the row includes an outgoing message id derived deterministically from stable incoming message and operation metadata

#### Scenario: Outbox operation is retried
- **WHEN** the same outbox row is dispatched more than once because a previous attempt failed or crashed before marking the row as dispatched
- **THEN** every dispatch attempt uses the same outgoing message id

#### Scenario: Multiple operations are captured for one incoming message
- **WHEN** one handler execution captures multiple outgoing operations
- **THEN** each operation receives a distinct deterministic outgoing message id

### Requirement: Outbox claim recovery is configurable
MiniBus SQL persistence SHALL make abandoned outbox claims eligible for retry after a configured claim lease duration.

#### Scenario: Claimed operation lease expires
- **WHEN** an outbox row is claimed but not marked dispatched or failed before the configured claim lease duration expires
- **THEN** a later dispatch cycle can claim the row again

#### Scenario: Claimed operation lease has not expired
- **WHEN** an outbox row is claimed and the configured claim lease duration has not elapsed
- **THEN** concurrent dispatch cycles do not claim the row

#### Scenario: Dispatch failure clears claim
- **WHEN** dispatching a claimed outbox operation fails and MiniBus records failure metadata
- **THEN** MiniBus clears the claim so the operation is eligible for a later retry according to normal pending-work selection

### Requirement: SQL persistence provides cleanup policies
MiniBus SQL persistence SHALL provide explicit cleanup behavior for old inbox records and outbox records using configured retention windows and batch limits.

#### Scenario: Inbox cleanup removes expired records
- **WHEN** SQL inbox cleanup runs with an inbox retention window
- **THEN** MiniBus removes processed-message records older than the retention cutoff up to the configured cleanup batch limit

#### Scenario: Dispatched outbox cleanup removes expired records
- **WHEN** SQL outbox cleanup runs with a dispatched outbox retention window
- **THEN** MiniBus removes dispatched outbox records older than the retention cutoff up to the configured cleanup batch limit

#### Scenario: Failed outbox cleanup is separately configured
- **WHEN** SQL outbox cleanup runs without a failed outbox retention window
- **THEN** MiniBus does not remove failed or undispatched outbox records solely because they are old

### Requirement: SQL schema changes are shipped as explicit scripts
MiniBus SQL persistence SHALL ship SQL Server/Azure SQL schema changes as explicit package scripts that applications can inspect and apply through their own deployment process.

#### Scenario: New schema is required
- **WHEN** a SQL persistence feature requires a schema change
- **THEN** MiniBus provides an additive versioned SQL script for that change

#### Scenario: Application uses custom schema or table names
- **WHEN** an application configures custom SQL schema or table names
- **THEN** MiniBus documents that packaged scripts target default names unless a customization path is explicitly provided

#### Scenario: Runtime starts
- **WHEN** MiniBus SQL persistence starts in an application
- **THEN** MiniBus does not automatically apply SQL schema migrations at runtime

### Requirement: SQL Server integration tests cover production persistence guarantees
MiniBus SHALL verify production SQL persistence guarantees with SQL Server/Azure SQL-compatible integration tests.

#### Scenario: Shared transaction behavior is verified
- **WHEN** SQL Server-backed integration tests run
- **THEN** they verify application-owned transaction commit and rollback behavior for business data, inbox records, and outbox records

#### Scenario: Deterministic outbox identity is verified
- **WHEN** SQL Server-backed integration tests dispatch or replay an outbox operation
- **THEN** they verify that the stored outgoing message id is stable across dispatch attempts

#### Scenario: Claim lease recovery is verified
- **WHEN** SQL Server-backed integration tests create a claimed-but-undispatched outbox operation older than the configured claim lease
- **THEN** they verify that a later dispatch cycle can reclaim and dispatch the operation

#### Scenario: Cleanup behavior is verified
- **WHEN** SQL Server-backed integration tests run cleanup for expired inbox and outbox records
- **THEN** they verify that expired eligible rows are removed and non-expired or ineligible rows remain

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

