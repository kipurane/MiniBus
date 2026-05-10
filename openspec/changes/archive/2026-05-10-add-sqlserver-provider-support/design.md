## Context

MiniBus now has a `MiniBus.Persistence.Sql` package with provider-neutral ADO.NET inbox/outbox behavior. Applications configure it by supplying a `DbConnection` factory, and the package owns the inbox/outbox schema script, session factory, outbox store, and dispatcher.

That shape proves the persistence model, but it leaves too much setup work for the primary target platform. SQL Server and Azure SQL are the planned production persistence stores, and MiniBus should provide a turnkey setup path that creates `SqlConnection` instances from connection strings while preserving the existing provider-neutral escape hatch.

## Goals / Non-Goals

**Goals:**
- Add first-class SQL Server/Azure SQL registration backed by `Microsoft.Data.SqlClient`.
- Allow applications to enable SQL inbox/outbox persistence with a connection string only.
- Preserve the existing caller-provided `DbConnection` factory path for custom connection ownership, tests, or future providers.
- Verify current schema and SQL statements against a real SQL Server-compatible database.
- Keep SQL-backed integration tests opt-in so normal local builds do not require SQL Server.
- Update docs and sample guidance with the new setup path.

**Non-Goals:**
- SQL saga persistence.
- Deterministic outgoing message ids for replay-safe outbox dispatch.
- Inbox/outbox cleanup and expiry policies.
- Framework-owned migrations beyond packaged SQL scripts.
- Shared transaction boundaries with application business data.
- Azure Functions processor pipeline refactor.
- Observability, logging scopes, tracing, or metrics.

## Decisions

### Add SqlClient support to the existing SQL persistence package

Prefer adding `Microsoft.Data.SqlClient` to `MiniBus.Persistence.Sql` instead of creating `MiniBus.Persistence.SqlServer` for this change. The current package is already named and documented as SQL Server/Azure SQL persistence, and its schema uses SQL Server constructs such as schemas, `SYSUTCDATETIME()`, `TOP`, `UPDLOCK`, and `READPAST`.

Alternative considered: create a separate provider package. That keeps the provider-neutral assembly free of concrete client dependencies, but the current package is not broadly relational-provider neutral in practice. A split would add packaging and documentation complexity without clear value for the first production target.

### Keep the factory path authoritative

Connection-string registration should populate a default `ConnectionFactory` that returns a new unopened `SqlConnection` for each persistence session or outbox store operation. Existing callers that set `ConnectionFactory` directly should continue to work, and custom factories should take precedence over connection-string construction when both are configured intentionally.

Alternative considered: store only a connection string and branch inside every SQL component. Centralizing connection creation behind the existing factory shape keeps session and outbox store code simple and keeps custom connection creation possible.

### Provide an explicit registration overload for connection strings

Add an overload or helper such as `AddMiniBusSqlPersistence(connectionString, configureOptions)` while retaining the current `Action<MiniBusSqlPersistenceOptions>` registration. The connection-string overload should make the common path obvious, while the options callback still configures schema names, table names, and dispatcher batch size.

Alternative considered: require setting `options.ConnectionString` in the existing callback. That avoids API growth, but the current option already contains a string that is not enough by itself. A dedicated overload makes the supported behavior harder to misread.

### Gate integration tests by environment configuration

SQL Server-backed tests should run only when a connection string is supplied through a documented environment variable such as `MINIBUS_SQLSERVER_TEST_CONNECTION_STRING`. When the variable is absent, the tests should skip rather than fail. Tests should create isolated schema/table names or an isolated database namespace per run so repeated local and CI executions do not collide.

Alternative considered: use a testcontainer by default. That gives the best repeatability but requires Docker availability, image downloads, and additional dependencies. An environment-gated connection string is lighter for this repo and can still work with local SQL Server, Azure SQL, or CI-provided SQL.

### Keep schema scripts as the migration surface

This change should verify and adjust the packaged schema script if needed, but not introduce a framework-owned migrator. The package should continue shipping explicit SQL scripts that applications can inspect and apply through their own deployment process.

Alternative considered: add a schema initializer service. That may be useful later, but it opens questions about permissions, migrations, rollout safety, and production ownership that are intentionally out of scope.

## Risks / Trade-offs

- [Risk] Adding `Microsoft.Data.SqlClient` increases package dependency weight for all `MiniBus.Persistence.Sql` consumers. → Mitigation: the package is already SQL Server/Azure SQL-oriented; preserve `ConnectionFactory` for advanced callers and revisit a separate provider package only if another SQL provider becomes a real target.
- [Risk] Integration tests can be flaky if they share tables or databases. → Mitigation: isolate schema/table names per test run and clean up where practical.
- [Risk] Tests silently skipping could hide SQL regressions. → Mitigation: make skip behavior explicit in test output and document the environment variable in README/test docs.
- [Risk] Connection-string registration could conflict with custom factory registration. → Mitigation: define precedence clearly and cover it with unit tests.
- [Risk] Real SQL Server may expose type or SQL syntax issues not covered by existing unit tests. → Mitigation: integration tests should cover schema application plus representative inbox, outbox, claim, dispatch metadata, and transaction paths.

## Migration Plan

1. Add SqlClient-backed connection factory support without changing existing direct-dispatch defaults.
2. Add connection-string registration API and tests for API/factory precedence behavior.
3. Verify packaged schema and current SQL statements against SQL Server; adjust scripts or parameter handling only where compatibility requires it.
4. Add opt-in SQL Server integration tests gated by the documented connection string environment variable.
5. Update README, SQL persistence docs, and sample guidance to show connection-string registration and integration test setup.
6. Rollback is removing the connection-string registration and SqlClient dependency; existing `DbConnection` factory registration remains the compatibility path.

## Open Questions

- What exact environment variable name should the SQL Server integration tests use?
- Should integration tests create unique schemas under the configured database, or require a disposable database?
- Should the connection-string overload accept only a string, or also a connection-string name/configuration callback for host configuration systems?
