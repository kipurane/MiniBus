## Why

MiniBus SQL persistence currently has a provider-neutral inbox/outbox foundation, but applications must still provide their own `DbConnection` factory. The next reliability step is to make SQL Server and Azure SQL turnkey through first-class SqlClient registration and SQL Server-backed verification.

## What Changes

- Add first-class Microsoft.Data.SqlClient-backed SQL Server/Azure SQL support, either in `MiniBus.Persistence.Sql` or a focused provider package if the design confirms package separation is cleaner.
- Add connection-string-based registration while preserving the existing caller-provided `DbConnection` factory escape hatch.
- Ensure SQL persistence sessions and outbox dispatch services can create and open `SqlConnection` instances from configured connection strings.
- Keep packaged SQL scripts as the migration/distribution mechanism unless SQL Server compatibility requires a small script adjustment.
- Add SQL Server-compatible integration test coverage for schema creation, inbox duplicate detection, outbox capture, outbox claim/dispatch/failure retry metadata, and MiniBus persistence transaction/commit behavior.
- Update README/docs/sample guidance to show the turnkey SQL Server/Azure SQL setup path.

No breaking changes are intended.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `sql-inbox-outbox`: Expand SQL persistence configuration and verification requirements to cover first-class SQL Server/Azure SQL connection-string setup and SQL Server-backed integration tests.

## Impact

- `MiniBus.Persistence.Sql` package registration APIs and dependencies, or a new SQL Server-specific persistence package if selected by design.
- SQL persistence options and connection creation behavior.
- SQL schema packaging and compatibility verification.
- SQL persistence test project, with integration tests gated by an opt-in connection string.
- README, package docs, and the Function App sample guidance where SQL persistence setup is discussed.
