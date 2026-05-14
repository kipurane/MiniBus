## Why

MiniBus already has transport-independent saga contracts and SQL inbox/outbox persistence, but saga state still relies on in-memory persistence for samples and tests. SQL-backed saga persistence is needed so long-running workflows can survive process restarts, scale-out execution, and retry/replay scenarios in production Azure Functions workloads.

## What Changes

- Add SQL Server / Azure SQL saga persistence to `MiniBus.Persistence.Sql` for loading, creating, saving, and completing saga data.
- Persist saga state with saga data type, correlation id, serialized data, completion state, timestamps, and optimistic concurrency metadata.
- Add a versioned SQL schema script for saga state storage without runtime migrations.
- Register SQL saga persistence through the existing SQL persistence dependency injection path.
- Preserve existing saga contracts and in-memory saga persistence behavior; SQL saga persistence remains opt-in through SQL persistence registration.
- Add SQL Server-compatible integration coverage for saga lifecycle, completion, optimistic concurrency, and serialization behavior.
- Document setup, schema application, and operational expectations for SQL-backed sagas.

## Capabilities

### New Capabilities
- `sql-saga-persistence`: SQL Server / Azure SQL persistence for durable MiniBus saga state, optimistic concurrency, completion handling, schema scripts, registration, and verification.

### Modified Capabilities

None.

## Impact

- `src/MiniBus.Persistence.Sql`: Add SQL saga persistence implementation, schema script, table name/options support, serializer usage, and DI registration.
- `src/MiniBus.Core`: Reuse existing saga contracts and `ISagaPersistence`; only adjust core abstractions if implementation reveals an unavoidable contract gap.
- `tests/MiniBus.Persistence.Sql.Tests`: Add SQL Server/Azure SQL integration tests for durable saga state and concurrency guarantees.
- `tests/MiniBus.Core.Tests`: Keep existing in-memory saga tests intact; add core tests only if shared saga behavior changes.
- Documentation and sample guidance: show SQL saga setup alongside SQL inbox/outbox setup and clarify that schema scripts are application-applied.
- SQL schema compatibility: ship saga tables as a new additive schema script rather than mutating previously shipped scripts in place.
