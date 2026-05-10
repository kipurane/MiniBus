## ADDED Requirements

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

### Requirement: SQL Server integration tests are opt-in
MiniBus SHALL provide SQL Server-compatible integration tests for SQL persistence behavior that run only when a SQL Server / Azure SQL test connection string is configured.

#### Scenario: Test connection string is configured
- **WHEN** the SQL persistence test suite runs with a documented SQL Server / Azure SQL test connection string
- **THEN** the suite verifies schema creation, inbox duplicate detection, outbox capture, outbox claim and dispatch state, failure retry metadata, and persistence transaction behavior against SQL Server-compatible storage

#### Scenario: Test connection string is absent
- **WHEN** the SQL persistence test suite runs without the documented SQL Server / Azure SQL test connection string
- **THEN** SQL Server-backed integration tests are skipped without failing the normal test run

### Requirement: SQL Server setup is documented
MiniBus SHALL document the SQL Server / Azure SQL setup path for SQL inbox/outbox persistence.

#### Scenario: Developer reads SQL persistence setup documentation
- **WHEN** a developer reads MiniBus SQL persistence documentation
- **THEN** it shows connection-string registration, schema script application, optional connection-factory registration, outbox dispatcher usage, and how to enable SQL Server-backed integration tests

## MODIFIED Requirements

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

### Requirement: SQL persistence behavior is documented and tested
MiniBus SHALL document SQL inbox/outbox setup and cover SQL persistence behavior with automated tests, including SQL Server-compatible integration tests when a test connection string is configured.

#### Scenario: Documentation shows setup
- **WHEN** a developer reads the SQL persistence documentation
- **THEN** it shows registration, schema setup, processing behavior, outbox dispatcher usage, and SQL Server/Azure SQL connection-string setup

#### Scenario: Tests cover core persistence behavior
- **WHEN** the normal test suite runs
- **THEN** it verifies duplicate detection, outbox capture, transactional commit, dispatch success, dispatch failure retry metadata, and opt-in behavior without requiring live SQL Server infrastructure

#### Scenario: Integration tests cover SQL Server behavior
- **WHEN** SQL Server-backed integration tests run with a configured test connection string
- **THEN** they verify schema creation, inbox duplicate detection, outbox capture, outbox claim and dispatch state, failure retry metadata, and persistence transaction behavior against SQL Server-compatible storage
