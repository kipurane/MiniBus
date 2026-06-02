## 1. Core Persistence Boundary

- [x] 1.1 Decide and document the session-bound saga persistence shape: reuse `ISagaPersistence` on active sessions or introduce a named session capability interface.
- [x] 1.2 Update saga invocation plumbing so SQL-backed processing uses the active persistence session for saga load/create/save/complete operations.
- [x] 1.3 Prevent silent fallback to independent saga persistence when a transactional SQL processing session is active but cannot provide saga persistence.
- [x] 1.4 Preserve existing non-transactional and standalone saga persistence behavior outside active processing sessions.

## 2. SQL Session Implementation

- [x] 2.1 Move shared SQL saga load/create/save/complete command logic into reusable helpers that can run on a supplied connection and transaction.
- [x] 2.2 Implement session-bound SQL saga load/create/save/complete operations on `SqlMiniBusPersistenceSession`.
- [x] 2.3 Ensure SQL saga create/save/complete uses the session's active transaction and rolls back with inbox/outbox work.
- [x] 2.4 Preserve SQL saga optimistic concurrency, duplicate create, missing saga, serialization, and completion behavior.
- [x] 2.5 Keep standalone `SqlSagaPersistence` behavior explicit and compatible for non-processing use.

## 3. Processing Behavior

- [x] 3.1 Verify pipeline ordering still begins persistence before handler and saga invocation.
- [x] 3.2 Ensure duplicate inbox detection short-circuits before saga invocation and saga mutation.
- [x] 3.3 Ensure saga persistence failures and concurrency conflicts flow through existing recoverability behavior.
- [x] 3.4 Ensure persistence commit failures are still propagated and do not settle the incoming message as complete.

## 4. Tests

- [x] 4.1 Add focused unit tests proving saga invocation uses session-bound saga persistence when an active processing session supports it.
- [x] 4.2 Add focused unit tests proving duplicate inbox short-circuit does not invoke sagas or mutate saga state.
- [x] 4.3 Add SQL integration coverage proving saga state creation rolls back when inbox/outbox commit fails.
- [x] 4.4 Add SQL integration coverage proving saga state save and completion roll back when outbox insertion or transaction commit fails.
- [x] 4.5 Add SQL integration coverage proving saga timeout scheduling commits saga state and scheduled outbox operation in the same transaction.
- [x] 4.6 Add SQL integration coverage proving stale saga version or duplicate saga create aborts processing without committing inbox/outbox/saga state.
- [x] 4.7 Re-run existing non-saga SQL inbox/outbox tests to verify compatibility.

## 5. Documentation and Validation

- [x] 5.1 Update saga documentation to state that saga mutations follow the durable processing outcome when transactional persistence is active.
- [x] 5.2 Update SQL persistence documentation to describe the atomic inbox/outbox/saga processing boundary and hosted-dispatch separation.
- [x] 5.3 Document standalone SQL saga persistence as separate from the atomic message-processing path.
- [x] 5.4 Document provider expectations for custom persistence implementations that want transactional saga guarantees.
- [x] 5.5 Add release and migration notes for the breaking `ISagaPersistence.SaveAsync`/`CompleteAsync` version-token contract change.
- [x] 5.6 Run OpenSpec validation for `reliable-saga-processing-boundary`.
- [x] 5.7 Run relevant Azure Functions and SQL persistence test suites.
