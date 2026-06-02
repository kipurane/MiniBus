## Context

MiniBus already has a strong SQL inbox/outbox reliability story: handler work can collect outgoing operations and `SqlMiniBusPersistenceSession` commits the inbox record and outbox rows together. Saga state is currently separate. `SagaInvoker` uses `ISagaPersistence`, and `SqlSagaPersistence` opens its own connection for load/create/save/complete operations. That means a saga can be saved or completed before the inbox/outbox transaction commits.

The desired operational model is simpler:

```text
received message
      |
      v
begin processing persistence session
      |
      v
invoke handlers and sagas
      |
      v
commit inbox + outbox + saga mutations
      |
      v
audit and settlement
```

For providers with transactional persistence, saga state must not have an earlier or separate durable outcome from the message being processed.

## Goals / Non-Goals

**Goals:**

- Make SQL-backed message processing commit inbox state, outbox operations, and saga state mutations in one SQL transaction.
- Keep existing non-saga SQL inbox/outbox behavior compatible.
- Preserve saga optimistic concurrency checks and failure flow through existing processing recoverability.
- Keep saga handlers storage-independent.
- Prefer a design that uses the active processing persistence session when one exists, instead of relying on a separate saga persistence singleton during processing.
- Add explicit failure-injection and integration coverage for rollback, conflicts, duplicate replay, and saga timeout scheduling.

**Non-Goals:**

- Do not split `MiniBus.Core` defaults or restructure packages.
- Do not redesign saga registration or activation unless required by the persistence boundary change.
- Do not change Service Bus settlement semantics beyond documenting how commit, audit, and settlement relate.
- Do not require non-transactional/custom persistence providers to promise SQL-style atomicity they cannot provide.

## Decisions

### Use the active processing persistence session as the reliable saga persistence path

During message processing, saga load/create/save/complete operations should use the active `IMiniBusPersistenceSession` when that session also supports saga persistence. For SQL, `SqlMiniBusPersistenceSession` should implement the saga persistence operations using its existing connection and active transaction.

Alternative considered: keep `SqlSagaPersistence` as the only saga persistence service and make it discover the active SQL transaction implicitly. That would hide the transaction dependency and make correctness depend on ambient state. Passing through the processing session keeps the durable boundary visible in the call graph.

Alternative considered: replace `ISagaPersistence` everywhere with a new session-only abstraction. That may become a good long-term cleanup, but it would broaden this reliability fix into a public API migration. This change can adapt the existing contract while still making the SQL processing path atomic.

### Keep standalone saga persistence for non-processing and compatibility scenarios

`SqlSagaPersistence` can remain available for direct lifecycle tests, administrative operations, and hosts that intentionally use saga persistence outside message processing. The processing path, however, must prefer the session-bound implementation when a persistence session is active.

Alternative considered: remove standalone SQL saga persistence registration. That would be cleaner in one sense, but it risks unnecessary breakage for existing tests and utility code. The reliability problem is specifically the message-processing path.

### Treat missing session-bound saga support as a provider capability decision

When a persistence session is active and saga processing requires transactional guarantees, the runtime should not silently fall back to an independent SQL saga persistence path. For SQL processing, missing session-bound saga persistence is a configuration/runtime error. For non-transactional custom providers, documentation must state that they either implement session-bound saga persistence or provide only eventual/custom consistency.

Alternative considered: always fall back to registered `ISagaPersistence`. That preserves compatibility but recreates the bug by allowing a separate durable saga outcome inside an otherwise transactional processing attempt.

### Preserve optimistic concurrency at the moment saga mutations are applied

SQL saga updates should continue to use rowversion metadata. If a stale saga version, duplicate create, or missing saga state is detected, saga invocation fails and normal recoverability handles the processing failure. Because the operation occurs inside the active transaction, any staged inbox/outbox/saga work rolls back with the processing attempt.

Alternative considered: defer all saga SQL writes until `CommitAsync`. That can work, but it makes conflict errors occur later and requires a new staging model. Applying saga SQL commands inside the active transaction is simpler and still invisible until commit.

### Keep audit outside this change, but document the boundary

Audit currently occurs after the processing commit and before settlement. This change should document that the atomic SQL processing guarantee covers inbox, outbox, and saga state, not audit writes or broker settlement. Audit failure behavior can remain as-is unless a separate change revisits audit reliability semantics.

## Risks / Trade-offs

- Custom persistence providers may currently rely on `IMiniBusPersistenceSession` containing only inbox/outbox behavior. → Use an optional session-bound saga persistence interface or existing `ISagaPersistence` implementation on the session rather than adding members directly to the public session interface.
- Silent fallback could reintroduce split persistence. → Make SQL transactional processing fail clearly when saga processing is enabled but the active SQL session cannot provide saga persistence.
- Applying saga writes inside the transaction can hold SQL locks longer than the current standalone saga writes. → Keep saga commands focused, preserve existing indexing by data type and correlation id, and cover concurrency behavior in integration tests.
- Direct `SqlSagaPersistence` remains capable of independent commits. → Document that it is not the reliable message-processing path when SQL inbox/outbox processing is active.
- Audit failure after a successful processing commit can still prevent settlement. → Document the boundary and leave any audit policy change to a dedicated proposal.

## Migration Plan

Existing applications using `AddMiniBusSqlPersistence` should receive the reliable processing behavior without code changes once the SQL session-bound saga path is implemented. Applications with custom persistence providers need to implement the session-bound saga persistence capability if they want transactional saga/inbox/outbox guarantees.

`ISagaPersistence.SaveAsync` and `ISagaPersistence.CompleteAsync` now require a non-null, non-whitespace `string version` token. This is a public breaking API change for downstream implementers and any direct callers that previously passed `null` for blind updates. Release notes must identify this as a breaking saga persistence contract change, with migration guidance to update method signatures, remove blind update semantics, load saga data before save/complete, and pass the returned opaque version token unchanged. If MiniBus is still pre-1.0 at release time, ship this in an explicitly called-out breaking preview release; if MiniBus is 1.0 or later, ship it in the next major version.

No SQL schema migration is expected if existing saga, inbox, and outbox tables already contain the required columns and rowversion metadata. Rollback is code-only: reverting the runtime package returns to the previous separate saga persistence behavior.

## Open Questions

- Should the session-bound saga persistence capability reuse `ISagaPersistence` directly on the active `IMiniBusPersistenceSession`, or should it introduce a named marker such as `IMiniBusSagaPersistenceSession` for clearer provider intent?
- Should SQL processing throw when saga processing is enabled but no session-bound saga persistence is available, or only when the configured saga provider is known to be SQL/transactional?
- Should documentation explicitly discourage using standalone `SqlSagaPersistence` from application handlers, even outside MiniBus processing?
