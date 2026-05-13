## Why

MiniBus already has SqlClient registration and SQL Server-backed verification, but the SQL persistence story still has production gaps around transaction ownership, replay-safe outbox dispatch, crash recovery, cleanup, and migration policy. Closing those gaps makes `MiniBus.Persistence.Sql` a first-class SQL Server/Azure SQL persistence option instead of only a working inbox/outbox foundation.

## What Changes

- Define how SQL persistence participates in application-owned business data transactions while preserving the existing MiniBus-owned transaction path.
- Add deterministic outgoing message ids for stored outbox operations so replayed dispatch attempts produce stable transport identities.
- Harden outbox crash recovery so claimed-but-not-dispatched operations become eligible for retry after a bounded lease timeout.
- Add cleanup and expiry policy support for inbox and outbox records.
- Confirm the migration distribution policy: MiniBus ships explicit SQL scripts instead of owning runtime database migrations.
- Extend SQL Server/Azure SQL integration coverage for shared transaction behavior, deterministic outbox message ids, claim lease recovery, outbox replay, and cleanup.
- Update documentation so the SQL Server/Azure SQL setup path describes transaction ownership, scripts, outbox replay behavior, and cleanup configuration.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `sql-inbox-outbox`: Complete the SQL persistence contract for transaction sharing, deterministic outbox identity, crash recovery, cleanup/expiry, script-based schema management, and SQL Server/Azure SQL verification coverage.

## Impact

- `src/MiniBus.Persistence.Sql`: SQL persistence options, session/transaction handling, outbox storage and dispatch behavior, schema scripts, and dependency injection registrations.
- `src/MiniBus.Core`: Persistence abstractions may need small additions if shared transaction ownership or deterministic operation identity requires framework-level contracts.
- `tests/MiniBus.Persistence.Sql.Tests`: Add SQL Server/Azure SQL integration coverage for the new persistence guarantees.
- `README.md`, `src/MiniBus.Persistence.Sql/README.md` if added, and sample guidance: document SQL script application, transaction options, replay behavior, and cleanup.
- SQL schema compatibility: changes may require a new additive schema script rather than mutating the existing packaged script in place.
