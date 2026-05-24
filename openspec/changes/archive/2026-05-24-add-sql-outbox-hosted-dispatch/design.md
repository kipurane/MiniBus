## Context

`MiniBus.Persistence.Sql` already provides durable inbox/outbox commit behavior and a manual `SqlMiniBusOutboxDispatcher` that claims pending rows and dispatches them through the configured transport. Applications that want automatic draining currently need to build that host behavior themselves, even though the repository already has the core dispatcher, claim/lease behavior, and SQL outbox dispatch metrics.

The design needs to add an in-process convenience path without weakening the current outbox contract. Durable commit remains the boundary: handler success means inbox state, saga state, and captured outbox operations are committed before the incoming message is completed, while transport dispatch remains a separate at-least-once step.

## Goals / Non-Goals

**Goals:**
- Provide an opt-in hosted-service API for automatic SQL outbox draining in common single-process deployments.
- Preserve manual `SqlMiniBusOutboxDispatcher` usage for tests, custom schedulers, and dedicated dispatcher processes.
- Keep current commit-first semantics intact for both MiniBus-owned and application-owned SQL transaction paths.
- Reuse the existing claim/lease model so multiple dispatcher instances can coexist safely.
- Define lifecycle behavior for polling, startup drain, failure backoff, graceful shutdown, and observability.

**Non-Goals:**
- Changing handler execution to dispatch outgoing transport work inline after the handler returns.
- Providing exactly-once transport delivery guarantees.
- Replacing external dispatcher processes or removing the manual dispatcher API.
- Introducing new SQL schema objects for hosted dispatch.

## Decisions

### 1. Add hosted dispatch as a separate opt-in registration surface

MiniBus will keep SQL persistence registration and hosted dispatch registration separate. `AddMiniBusSqlPersistence(...)` will continue to register persistence primitives and `SqlMiniBusOutboxDispatcher`, while a new opt-in registration method will add the hosted background dispatch service and its options.

This keeps storage capability separate from hosting policy. Applications that want manual dispatch only, a timer-triggered drain, or a separate worker process do not pay for a background loop they did not request.

Alternatives considered:
- Add a boolean like `AutoDispatch = true` to `MiniBusSqlPersistenceOptions`: rejected because it couples storage registration and host lifecycle policy into one options object.
- Enable hosted dispatch by default whenever SQL persistence is registered: rejected because it would change application behavior implicitly and make tests and dedicated dispatcher processes harder to control.

### 2. The hosted service will wrap the existing dispatcher with bounded dispatch cycles

The hosted service will resolve `SqlMiniBusOutboxDispatcher` and execute dispatch cycles in a loop. Each cycle will call `DispatchPendingAsync` up to a configured maximum number of batches, stopping early when a batch dispatches zero operations.

The hosted options will cover:
- poll interval between idle cycles
- maximum batches per cycle
- failure backoff after dispatch loop errors
- optional drain on startup

Hosted options will validate values at registration time. Poll interval, maximum batches per cycle, and failure backoff must be positive, and defaults will be conservative enough for local development without creating an aggressive idle poll loop.

The hosted loop needs enough per-cycle result metadata to distinguish an empty dispatch from a dispatch cycle that claimed work but failed every operation. The public manual dispatcher API may remain `DispatchPendingAsync(...) -> int` for compatibility, but the hosted service must not infer "idle" solely from a zero dispatched count if failed work was attempted. Implementation can satisfy this by adding an internal result shape, exposing richer dispatcher metadata carefully, or otherwise letting the hosted loop observe claimed/dispatched/failed counts without duplicating dispatch logic.

This keeps dispatch behavior centered on the existing claim/dispatch/mark-dispatched flow instead of adding a second dispatch implementation.

Alternatives considered:
- One batch per timer tick only: simpler, but increases latency and backlog drain time under sustained load.
- Drain until empty with no batch cap: rejected because a single host could monopolize work and delay shutdown or configuration responsiveness.
- Treat `DispatchPendingAsync` returning zero as always idle: rejected because zero dispatched can also mean claimed operations all failed, which should feed failure diagnostics/backoff rather than quiet idle polling.

### 3. Low-latency wake-up will be best-effort, not the correctness mechanism

When hosted dispatch is enabled, MiniBus will add a local wake-up signal that can request an earlier dispatch cycle after new outbox work is committed in a MiniBus-owned transaction. Polling remains the correctness baseline for crash recovery, multi-instance discovery, and application-owned transaction paths.

The wake-up signal fires only after the MiniBus-owned SQL transaction commits successfully. If the MiniBus-owned commit fails or rolls back, no wake-up is sent. For application-owned SQL transactions, MiniBus will not try to infer the outer transaction's final commit moment; those paths will rely on polling unless the application chooses to trigger manual dispatch separately.

Alternatives considered:
- No wake-up support at all: valid, but adds avoidable latency for the common in-process hosted scenario.
- Treat wake-up as required for correctness: rejected because signals are process-local and do not survive crashes or help other instances discover work.
- Fire wake-up whenever outbox rows are inserted: rejected because rows inserted inside an application-owned transaction may still roll back, and signaling before durable commit can produce confusing empty dispatch cycles.

### 4. Observability will extend the current dispatch story instead of replacing it

Hosted dispatch will continue to use the existing SQL outbox dispatch metrics emitted by `SqlMiniBusOutboxDispatcher`. The hosted loop will add structured logs around startup, shutdown, wake-up, idle polling, failure backoff, and unexpected loop termination so operators can distinguish transport-dispatch failures from host-lifecycle issues.

This avoids splitting batch and operation metrics across multiple instrumentation paths while still making the hosted loop diagnosable.

Alternatives considered:
- Add a second set of hosted-loop metrics for every dispatch outcome: deferred unless logs and the existing dispatch metrics prove insufficient.

### 5. Shutdown behavior will favor recoverability over aggressive draining

During host shutdown, the hosted service will stop scheduling new cycles and pass cancellation into any active dispatch call. Any rows left pending or claimed but not fully dispatched remain recoverable through the existing claim-lease expiry behavior.

This keeps shutdown predictable and preserves the current at-least-once recovery model.

Alternatives considered:
- Force a final drain-until-empty on shutdown: rejected because it can prolong shutdown indefinitely and still cannot guarantee that every accepted transport operation is marked dispatched before process termination.

## Risks / Trade-offs

- [Background loop increases host complexity] → Keep the API opt-in, bounded, and reuse the existing dispatcher instead of creating parallel dispatch logic.
- [Best-effort wake-up behaves differently for MiniBus-owned and application-owned transactions] → Document the distinction clearly and keep polling as the universal recovery path.
- [Misconfigured polling or batch limits can increase latency] → Provide conservative defaults and document how latency and throughput trade off against each other.
- [Multiple instances can still produce at-least-once duplicates after crash windows] → Preserve deterministic outgoing message ids, broker duplicate detection guidance, and inbox/idempotent receiver guidance.
- [Hosted dispatch logs may be noisier in idle systems] → Keep idle logging low-volume and focus detailed logs on state transitions and failures.

## Migration Plan

This change is additive. Existing applications keep manual dispatch behavior unless they opt into hosted dispatch registration. Rollback is straightforward: remove the hosted dispatch registration and return to manual or external dispatcher ownership.

## Open Questions

- None for proposal readiness. If implementation feedback shows operators need separate host-loop metrics beyond the existing outbox dispatch instruments, that can be evaluated during implementation without changing the core contract.
