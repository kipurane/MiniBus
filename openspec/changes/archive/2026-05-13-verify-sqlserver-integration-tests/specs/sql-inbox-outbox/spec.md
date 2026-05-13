## MODIFIED Requirements

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

### Requirement: SQL Server setup is documented
MiniBus SHALL document the SQL Server / Azure SQL setup path and the Testcontainers-backed verification path for SQL inbox/outbox persistence.

#### Scenario: Developer reads SQL persistence setup documentation
- **WHEN** a developer reads MiniBus SQL persistence documentation
- **THEN** it shows connection-string registration, schema script application, optional connection-factory registration, outbox dispatcher usage, Testcontainers-backed integration test execution, Apple Silicon Docker requirements, and the external connection string fallback
