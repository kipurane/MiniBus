## 1. Hosted Dispatch API

- [x] 1.1 Add a hosted SQL outbox dispatch options type and opt-in registration API that composes with `AddMiniBusSqlPersistence(...)` without changing manual-dispatch defaults, including documented defaults and validation for polling interval, maximum batches per cycle, failure backoff, and startup drain behavior.
- [x] 1.2 Register the hosted dispatch dependencies and background service while keeping `SqlMiniBusOutboxDispatcher` available for manual, test, and external dispatcher use.

## 2. Hosted Dispatch Loop

- [x] 2.1 Implement the hosted dispatch loop around `SqlMiniBusOutboxDispatcher` with polling, bounded batches per cycle, optional startup drain, and failure backoff, using enough dispatch-cycle result metadata to distinguish empty batches from claimed-but-failed work.
- [x] 2.2 Add best-effort in-process wake-up after successful MiniBus-owned SQL commits without making correctness depend on the wake-up path or changing application-owned transaction semantics.
- [x] 2.3 Add structured hosted-dispatch logging for startup, idle polling, wake-up, failures, backoff, and shutdown while reusing the existing SQL outbox dispatch metrics.

## 3. Verification And Documentation

- [x] 3.1 Add automated tests for opt-in behavior, manual dispatcher availability, option validation, multi-batch draining, failure backoff, dispatch-cycle result handling, and graceful shutdown or recovery.
- [x] 3.2 Extend SQL-backed integration or acceptance coverage to verify hosted-dispatch compatibility with the existing claim-lease and at-least-once behavior.
- [x] 3.3 Add tests proving wake-up fires after successful MiniBus-owned commits, does not fire after MiniBus-owned commit failure, and does not fire for application-owned transactions whose commit moment MiniBus does not own.
- [x] 3.4 Update SQL persistence and host-facing documentation to show manual versus hosted dispatch setup and explain why dispatch remains separate from handler execution.
