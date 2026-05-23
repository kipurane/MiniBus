## 1. Hosted Dispatch API

- [ ] 1.1 Add a hosted SQL outbox dispatch options type and opt-in registration API that composes with `AddMiniBusSqlPersistence(...)` without changing manual-dispatch defaults.
- [ ] 1.2 Register the hosted dispatch dependencies and background service while keeping `SqlMiniBusOutboxDispatcher` available for manual, test, and external dispatcher use.

## 2. Hosted Dispatch Loop

- [ ] 2.1 Implement the hosted dispatch loop around `SqlMiniBusOutboxDispatcher` with polling, bounded batches per cycle, optional startup drain, and failure backoff.
- [ ] 2.2 Add best-effort in-process wake-up for MiniBus-owned SQL commits without making correctness depend on the wake-up path or changing application-owned transaction semantics.
- [ ] 2.3 Add structured hosted-dispatch logging for startup, idle polling, wake-up, failures, backoff, and shutdown while reusing the existing SQL outbox dispatch metrics.

## 3. Verification And Documentation

- [ ] 3.1 Add automated tests for opt-in behavior, manual dispatcher availability, multi-batch draining, failure backoff, and graceful shutdown or recovery.
- [ ] 3.2 Extend SQL-backed integration or acceptance coverage to verify hosted-dispatch compatibility with the existing claim-lease and at-least-once behavior.
- [ ] 3.3 Update SQL persistence and host-facing documentation to show manual versus hosted dispatch setup and explain why dispatch remains separate from handler execution.