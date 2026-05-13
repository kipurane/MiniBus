## Context

`MiniBus.Persistence.Sql` now has first-class SQL Server/Azure SQL registration and an opt-in SQL Server integration test path keyed by `MINIBUS_SQLSERVER_TEST_CONNECTION_STRING`. That verifies the code when a database is manually supplied, but it leaves the most important SQL compatibility check easy to skip.

This change makes SQL Server-backed verification self-contained for developers and CI by using Testcontainers. Current Testcontainers for .NET guidance provides an MSSQL module using Microsoft SQL Server container images such as `mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04`. Azure SQL Edge should not be used as the Apple Silicon answer because it is retired and no longer supports ARM64.

## Goals / Non-Goals

**Goals:**
- Add Testcontainers-based SQL Server integration testing for `MiniBus.Persistence.Sql`.
- Keep the existing `MINIBUS_SQLSERVER_TEST_CONNECTION_STRING` override for environments that provide their own SQL Server/Azure SQL instance.
- Support Apple Silicon developer machines by explicitly configuring or documenting the SQL Server container platform/image behavior for Docker Desktop.
- Keep SQL integration tests skippable with a clear reason when neither Docker nor an external connection string is available.
- Preserve the current SQL persistence behavior under test: schema creation, inbox duplicate detection, outbox capture, outbox claim/dispatch metadata, failure retry metadata, and transaction rollback.

**Non-Goals:**
- Production code changes to SQL persistence APIs.
- Replacing SQL Server with Azure SQL Edge.
- Requiring Docker for the normal unit test suite when SQL integration tests are intentionally skipped.
- Adding a full CI workflow unless the implementation environment already has one.
- Benchmarking SQL Server container startup or tuning test performance beyond reasonable fixture reuse.

## Decisions

### Use Testcontainers.MsSql as the primary integration test provider

Use the Testcontainers for .NET MSSQL module instead of hand-rolling Docker process management. The module owns container lifecycle, readiness, connection-string generation, EULA configuration, and cleanup.

Alternative considered: continue with only `MINIBUS_SQLSERVER_TEST_CONNECTION_STRING`. That is useful as an override, but it keeps verification dependent on manual infrastructure.

### Use a Microsoft SQL Server image, not Azure SQL Edge

Use a SQL Server image compatible with the Testcontainers MSSQL module, such as `mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04` or a newer pinned SQL Server 2022 Ubuntu image chosen at implementation time. Do not use Azure SQL Edge: it is retired and no longer supports ARM64.

Alternative considered: use Azure SQL Edge because it was historically common for Apple Silicon. That would bake a retired/deprecated image into the test strategy and would not satisfy current ARM64 support constraints.

### Treat Apple Silicon as an explicit platform compatibility case

On Apple Silicon, SQL Server containers generally run through Docker Desktop's `linux/amd64` support rather than a native ARM64 SQL Server engine image. The test fixture should either configure the SQL Server container platform to `linux/amd64` when needed, or document the Docker Desktop setting required for x86_64/amd64 emulation. If Docker cannot run the image, tests should skip with a clear message instead of failing the whole suite.

Alternative considered: require a native ARM64 SQL Server-compatible image. That is not a reliable current assumption, and the project should not anchor on Azure SQL Edge.

### Keep external connection string override

When `MINIBUS_SQLSERVER_TEST_CONNECTION_STRING` is set, integration tests should use that database instead of starting a container. This supports CI environments where SQL Server is provisioned as a service and developers who want to test against Azure SQL or a local instance.

Alternative considered: always use Testcontainers. That is simpler but less flexible for CI, restricted Docker environments, and Azure SQL compatibility checks.

### Reuse container/database setup across tests where practical

Use a shared fixture to avoid starting one SQL Server container per test. Each test should still use isolated schemas or table names so test cases do not interfere with each other.

Alternative considered: one container per test. That maximizes isolation but makes the suite slow and brittle, especially under amd64 emulation on Apple Silicon.

## Risks / Trade-offs

- [Risk] SQL Server containers can be slow under Apple Silicon amd64 emulation. → Mitigation: use a shared fixture and isolate schemas per test rather than starting many containers.
- [Risk] Docker may not be installed or may not support the required platform. → Mitigation: skip SQL Server integration tests with a clear message unless an external connection string is configured.
- [Risk] Image tags can drift or disappear. → Mitigation: pin a known SQL Server 2022 Ubuntu tag and document the update path.
- [Risk] Testcontainers introduces another test dependency. → Mitigation: keep it scoped to `MiniBus.Persistence.Sql.Tests` and only for integration tests.
- [Risk] EULA acceptance requirements can surprise contributors. → Mitigation: use the MSSQL module's supported license acceptance path and document it.

## Migration Plan

1. Add Testcontainers MSSQL dependencies to the SQL persistence test project.
2. Replace or extend the existing SQL Server integration fixture so it uses an external connection string when configured, otherwise starts a Testcontainers-managed SQL Server container.
3. Add Apple Silicon/platform handling or documentation for Docker Desktop amd64 execution.
4. Preserve clear skip behavior when Docker/container startup is unavailable.
5. Run the SQL Server-backed integration tests through Testcontainers and update the previously deferred verification task.
6. Update README/test documentation with Testcontainers-first instructions and the external connection string override.

## Open Questions

- Which exact SQL Server 2022 container tag should be pinned during implementation?
- Should Apple Silicon platform selection be automatic from `RuntimeInformation.ProcessArchitecture`, or documented as a Docker Desktop prerequisite?
- Should SQL Server integration tests run by default in CI if Docker is available, or stay opt-in through a test filter/environment variable?
