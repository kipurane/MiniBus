## ADDED Requirements

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
