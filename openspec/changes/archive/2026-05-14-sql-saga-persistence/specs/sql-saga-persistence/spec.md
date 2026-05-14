## ADDED Requirements

### Requirement: SQL saga persistence package isolates storage dependencies
MiniBus SHALL provide SQL Server / Azure SQL saga persistence in the SQL persistence package without requiring saga handlers or saga data classes to reference SQL client APIs.

#### Scenario: Saga handlers remain storage independent
- **WHEN** an application enables SQL saga persistence
- **THEN** saga handlers and saga data continue to depend only on MiniBus saga contracts and `MiniBusContext`

#### Scenario: Existing saga contracts are reused
- **WHEN** SQL saga persistence is enabled
- **THEN** MiniBus uses the existing `ISagaPersistence` contract for loading, creating, saving, and completing saga data

### Requirement: SQL schema stores saga state
MiniBus SQL persistence SHALL define schema objects for storing saga data by saga data type and correlation id.

#### Scenario: Saga state is stored
- **WHEN** saga data is created through SQL saga persistence
- **THEN** MiniBus stores the saga id, saga data type, correlation id, serialized data, completion state, created timestamp, updated timestamp, and optimistic concurrency metadata

#### Scenario: Duplicate saga correlation is rejected
- **WHEN** SQL saga persistence creates saga data for a saga data type and correlation id that already exists
- **THEN** MiniBus rejects the create operation with a saga persistence failure

### Requirement: SQL saga persistence loads saga data
MiniBus SQL persistence SHALL load saga data by saga data type and correlation id.

#### Scenario: Existing saga data is loaded
- **WHEN** saga data exists for the requested saga data type and correlation id
- **THEN** MiniBus returns the deserialized saga data with version metadata

#### Scenario: Missing saga data returns no record
- **WHEN** no saga data exists for the requested saga data type and correlation id
- **THEN** MiniBus returns no saga persistence record

### Requirement: SQL saga persistence saves saga data with optimistic concurrency
MiniBus SQL persistence SHALL save existing saga data only when the expected version matches the stored version.

#### Scenario: Save succeeds with current version
- **WHEN** saga data is saved with the version returned by the latest load operation
- **THEN** MiniBus updates the serialized data, completion state, updated timestamp, and version metadata

#### Scenario: Save fails with stale version
- **WHEN** saga data is saved with a stale version
- **THEN** MiniBus rejects the save operation with a saga persistence failure

#### Scenario: Save fails for missing saga
- **WHEN** saga data is saved for a saga data type and correlation id that does not exist
- **THEN** MiniBus rejects the save operation with a saga persistence failure

### Requirement: SQL saga persistence completes saga data
MiniBus SQL persistence SHALL persist completed saga state as terminal saga state.

#### Scenario: Saga completion is persisted
- **WHEN** saga data is completed with the current version
- **THEN** MiniBus marks the saga data as completed, stores the serialized completed state, records the completion timestamp, and advances the version metadata

#### Scenario: Completed saga data is loaded
- **WHEN** completed saga data is loaded by saga data type and correlation id
- **THEN** MiniBus returns saga data whose completion flag is set

#### Scenario: Completion fails with stale version
- **WHEN** saga data is completed with a stale version
- **THEN** MiniBus rejects the completion operation with a saga persistence failure

### Requirement: SQL saga persistence uses configured serialization
MiniBus SQL persistence SHALL serialize and deserialize saga data using the configured MiniBus serializer behavior.

#### Scenario: Saga data is serialized
- **WHEN** SQL saga persistence stores saga data
- **THEN** MiniBus serializes the concrete saga data type into the SQL saga record

#### Scenario: Saga data is deserialized
- **WHEN** SQL saga persistence loads saga data
- **THEN** MiniBus deserializes the stored payload as the requested saga data type

### Requirement: SQL saga persistence is configured through dependency injection
MiniBus SQL persistence SHALL provide dependency injection registration for SQL saga persistence using the existing SQL Server / Azure SQL connection settings and schema options.

#### Scenario: Application enables SQL persistence
- **WHEN** an application registers MiniBus SQL persistence with SQL saga support available
- **THEN** MiniBus can resolve `ISagaPersistence` as the SQL-backed implementation

#### Scenario: Azure Functions fallback does not override SQL saga persistence
- **WHEN** an application registers Azure Functions processing and SQL persistence in either common order
- **THEN** MiniBus uses the configured SQL saga persistence provider instead of the unconfigured fallback provider

#### Scenario: SQL persistence is not enabled
- **WHEN** an application does not register SQL saga persistence
- **THEN** MiniBus preserves the existing non-SQL saga persistence behavior

### Requirement: SQL saga schema changes are shipped as explicit scripts
MiniBus SQL persistence SHALL ship SQL Server / Azure SQL saga schema changes as explicit package scripts that applications can inspect and apply through their own deployment process.

#### Scenario: Saga schema is required
- **WHEN** an application wants to use SQL saga persistence
- **THEN** MiniBus provides an additive versioned SQL script for the saga table and related indexes

#### Scenario: Runtime starts
- **WHEN** MiniBus SQL persistence starts in an application
- **THEN** MiniBus does not automatically apply SQL saga schema migrations at runtime

### Requirement: SQL saga persistence behavior is documented and tested
MiniBus SHALL document SQL saga persistence setup and cover SQL saga persistence behavior with automated tests.

#### Scenario: Documentation shows setup
- **WHEN** a developer reads the SQL persistence documentation
- **THEN** it shows SQL saga registration, schema script application, table configuration, serialization behavior, completion behavior, and optimistic concurrency expectations

#### Scenario: Integration tests cover saga lifecycle
- **WHEN** SQL Server-backed integration tests run through Testcontainers or a configured test connection string
- **THEN** they verify schema creation, saga create, load, save, complete, duplicate create rejection, stale version rejection, and serialization behavior against SQL Server-compatible storage
