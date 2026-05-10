## 1. Package and API Setup

- [x] 1.1 Add `Microsoft.Data.SqlClient` support to the selected SQL persistence package.
- [x] 1.2 Add a connection-string-based SQL persistence registration API while preserving the existing options callback registration.
- [x] 1.3 Implement SqlClient-backed connection factory creation from configured SQL Server/Azure SQL connection strings.
- [x] 1.4 Define and test precedence between explicit `DbConnection` factories and connection-string-backed factories.

## 2. SQL Persistence Compatibility

- [x] 2.1 Verify persistence sessions open SqlClient connections correctly for inbox lookup and commit operations.
- [x] 2.2 Verify outbox store operations open SqlClient connections correctly for claim, mark-dispatched, and mark-failed operations.
- [x] 2.3 Review packaged schema and current SQL statements for SQL Server/Azure SQL compatibility, adjusting only where required.
- [x] 2.4 Preserve schema name, inbox table name, outbox table name, and dispatcher batch size options in the new registration path.

## 3. Integration Test Infrastructure

- [x] 3.1 Add a documented SQL Server test connection string mechanism for opt-in integration tests.
- [x] 3.2 Add test helpers that create isolated schema/table names or an isolated database namespace per test run.
- [x] 3.3 Ensure SQL Server-backed tests skip clearly when the test connection string is absent.
- [x] 3.4 Ensure SQL Server-backed tests clean up created schema objects where practical.

## 4. SQL Server Integration Coverage

- [x] 4.1 Add integration tests that apply the packaged schema script against SQL Server-compatible storage.
- [x] 4.2 Add integration tests for inbox processed-message recording and duplicate detection.
- [x] 4.3 Add integration tests for outbox operation capture during commit.
- [x] 4.4 Add integration tests for outbox claim, mark-dispatched, mark-failed, attempt count, and last error metadata.
- [x] 4.5 Add integration tests for transaction behavior when commit succeeds and when commit fails.

## 5. Documentation and Samples

- [x] 5.1 Update README or SQL persistence docs with connection-string registration.
- [x] 5.2 Document the packaged schema script application flow for SQL Server/Azure SQL.
- [x] 5.3 Document the existing `DbConnection` factory escape hatch for custom connection ownership.
- [x] 5.4 Update sample guidance to show where the new SQL persistence setup path fits.
- [x] 5.5 Document how to enable SQL Server-backed integration tests locally or in CI.

## 6. Verification

- [x] 6.1 Run the normal unit test suite without a SQL Server connection string.
- [ ] 6.2 Run SQL Server-backed integration tests with a configured test connection string.
- [x] 6.3 Run OpenSpec validation for `add-sqlserver-provider-support`.
