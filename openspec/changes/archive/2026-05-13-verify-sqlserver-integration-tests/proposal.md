## Why

The SQL Server provider change added opt-in integration tests, but the live SQL Server verification task was deferred because no SQL Server connection string was configured. MiniBus should make this verification repeatable by default with Testcontainers while still supporting an externally supplied SQL Server/Azure SQL connection string.

## What Changes

- Replace the manual-only SQL Server integration test path with Testcontainers-based SQL Server tests.
- Use the Testcontainers for .NET MSSQL module and a Microsoft SQL Server container image.
- Support Apple Silicon development machines by explicitly handling the SQL Server container platform/image choice for Docker Desktop on arm64 hosts.
- Keep an external `MINIBUS_SQLSERVER_TEST_CONNECTION_STRING` override for environments that cannot or should not run Docker containers.
- Run the full SQL Server-backed persistence coverage against the Testcontainers-managed database by default when Docker is available.
- Document Docker/Testcontainers prerequisites, EULA acceptance, Apple Silicon behavior, and the external connection string fallback.

No production API changes are intended.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `sql-inbox-outbox`: Strengthen SQL Server integration test requirements so the SQL persistence test suite can provision SQL Server automatically with Testcontainers and can still fall back to a configured external connection string.

## Impact

- `tests/MiniBus.Persistence.Sql.Tests` gains Testcontainers dependencies and shared SQL Server container test infrastructure.
- SQL Server integration tests move from "skip unless connection string exists" to "use Testcontainers when Docker is available, otherwise skip with a clear reason unless an external connection string is configured."
- Documentation and README guidance for running SQL persistence tests changes from connection-string-only to Testcontainers-first with an override.
- CI may optionally run these tests wherever Docker Linux containers are available.
