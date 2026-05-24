## Why

MiniBus.Persistence.Sql currently requires applications to invoke `SqlMiniBusOutboxDispatcher.DispatchPendingAsync` explicitly to move durable outbox work from SQL storage to the configured transport. That keeps the outbox boundary explicit, but it leaves applications without a built-in host-managed option for low-latency automatic draining in common single-process deployments.

## What Changes

- Add an opt-in hosted-service dispatching path for SQL outbox operations so applications can enable automatic background draining inside the same process.
- Preserve the current outbox contract: successful handler execution still commits inbox state, saga state, and captured outbox operations before the incoming message is treated as complete.
- Preserve manual dispatch through `SqlMiniBusOutboxDispatcher` for tests, custom schedulers, and dedicated dispatcher processes.
- Define hosted dispatch configuration for polling cadence, per-cycle batch limits, startup drain behavior, backoff after failures, and optional low-latency wake-up without making wake-up the correctness mechanism.
- Document and test multi-instance safety, shutdown behavior, replay-safe recovery, and the continued at-least-once nature of outbox dispatch.

## Capabilities

### New Capabilities
None.

### Modified Capabilities
- `sql-inbox-outbox`: add optional hosted-service registration and lifecycle behavior for automatic SQL outbox draining while preserving manual dispatch and current durability guarantees.

## Impact

- Affected packages: `MiniBus.Persistence.Sql` and any hosting-facing documentation that shows SQL outbox usage. The SQL package may need a hosting abstraction dependency, such as `Microsoft.Extensions.Hosting.Abstractions`, for `IHostedService` registration.
- Affected APIs: SQL persistence dependency injection surface and new hosted-dispatch configuration API.
- Affected behavior: applications may opt into background outbox draining, but processing guarantees remain commit-first and dispatch-later.
- Affected tests: SQL persistence and acceptance coverage for manual mode, hosted automatic mode, failure backoff, recovery, and graceful shutdown.
