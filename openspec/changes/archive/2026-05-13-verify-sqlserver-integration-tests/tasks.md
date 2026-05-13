## 1. Testcontainers Setup

- [x] 1.1 Add the Testcontainers MSSQL module dependency to `tests/MiniBus.Persistence.Sql.Tests`.
- [x] 1.2 Choose and pin a SQL Server 2022 container image compatible with the Testcontainers MSSQL module.
- [x] 1.3 Configure license/EULA acceptance through the Testcontainers MSSQL module.
- [x] 1.4 Preserve `MINIBUS_SQLSERVER_TEST_CONNECTION_STRING` as an external database override.

## 2. Container Fixture

- [x] 2.1 Refactor SQL Server integration test infrastructure to use a shared Testcontainers SQL Server fixture when no external connection string is set.
- [x] 2.2 Add Apple Silicon handling for the SQL Server container platform/image strategy, or document the Docker Desktop prerequisite in code-adjacent test messages.
- [x] 2.3 Ensure tests skip clearly when Docker is unavailable or cannot run the SQL Server image.
- [x] 2.4 Keep isolated schemas or table names per test so shared container execution remains independent.
- [x] 2.5 Ensure containers and schema objects are cleaned up after tests.

## 3. Integration Coverage

- [x] 3.1 Run schema creation tests against the Testcontainers-managed SQL Server database.
- [x] 3.2 Run inbox duplicate detection tests against the Testcontainers-managed SQL Server database.
- [x] 3.3 Run outbox capture tests against the Testcontainers-managed SQL Server database.
- [x] 3.4 Run outbox claim, dispatch state, and failure retry metadata tests against the Testcontainers-managed SQL Server database.
- [x] 3.5 Run commit success and rollback tests against the Testcontainers-managed SQL Server database.
- [x] 3.6 Verify the same tests still support the external connection string override.

## 4. Documentation

- [x] 4.1 Update SQL persistence documentation to describe Testcontainers-backed integration tests.
- [x] 4.2 Document Apple Silicon Docker Desktop requirements or platform behavior for SQL Server containers.
- [x] 4.3 Document the external `MINIBUS_SQLSERVER_TEST_CONNECTION_STRING` fallback.
- [x] 4.4 Document how CI can run or skip SQL Server integration tests.

## 5. Verification

- [x] 5.1 Run the normal unit test suite without requiring SQL Server.
- [x] 5.2 Run SQL Server-backed integration tests through Testcontainers.
- [ ] 5.3 Run SQL Server-backed integration tests with `MINIBUS_SQLSERVER_TEST_CONNECTION_STRING` if an external SQL Server/Azure SQL database is available.
- [x] 5.4 Run OpenSpec validation for `verify-sqlserver-integration-tests`.
