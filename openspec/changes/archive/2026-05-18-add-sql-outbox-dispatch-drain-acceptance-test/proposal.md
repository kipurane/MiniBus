## Why

The SQL-backed reference acceptance test currently proves that the billing workflow captures durable inbox, outbox, and saga state, but it stops before the persisted outbox is drained. This leaves the highest-level production reliability story partially unproven: captured outgoing work must be dispatchable through the configured transport and then marked dispatched in SQL.

## What Changes

- Add a Tier 2 SQL-backed acceptance scenario that processes the reference billing workflow, runs `SqlMiniBusOutboxDispatcher.DispatchPendingAsync`, and verifies the recording transport receives the expected send, publish, and schedule operations.
- Verify drained outbox rows are marked dispatched and no longer appear as pending/reclaimable work.
- Reuse existing SQL acceptance infrastructure, sample-style billing registration, and recording transport/settlement doubles.
- Preserve the existing SQL test environment behavior: use Testcontainers SQL Server or `MINIBUS_SQLSERVER_TEST_CONNECTION_STRING`, and skip clearly when neither is available.
- Keep live Azure Service Bus integration tests out of scope.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `reference-solution-acceptance-tests`: Extend the SQL-backed reference scenario to prove persisted outbox work can be drained through the configured transport and marked dispatched.

## Impact

- Affected tests: `tests/MiniBus.AcceptanceTests/ReferenceSolutionAcceptanceTests.cs`.
- Affected specs: `openspec/specs/reference-solution-acceptance-tests/spec.md`.
- No expected runtime API, package dependency, schema, or production behavior changes.
