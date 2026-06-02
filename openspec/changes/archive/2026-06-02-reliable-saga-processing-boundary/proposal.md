## Why

MiniBus SQL-backed processing currently persists saga state through a separate saga persistence path from the inbox/outbox commit, which can make saga state visible even when the message processing transaction later fails. This change makes the reliable processing model explicit and hard to misuse: a processed message has one durable outcome for inbox state, outbox operations, and saga state.

## What Changes

- Change SQL-backed saga processing so saga state changes participate in the same durable processing boundary as inbox and outbox operations.
- Require transactional persistence providers to commit inbox records, outbox operations, and saga mutations atomically for a successful processing attempt.
- Preserve saga optimistic concurrency semantics while moving saga create/save/complete work under the active processing persistence session when one exists.
- Define duplicate/replay behavior for saga messages so skipped duplicates do not invoke saga handlers or mutate saga state.
- Define failure behavior for saga conflicts, saga persistence failures, outbox insertion failures, and commit failures.
- Add failure-injection and SQL integration coverage proving saga state rolls back when the processing commit fails.
- Clarify documentation for the atomic processing guarantee and provider limitations.
- Capture saga activation and parameterless-construction coupling as a follow-up concern unless the implementation must touch it to make the persistence boundary reliable.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `basic-saga-support`: Saga persistence semantics must align with the processing outcome so saga mutations are durable only when the processing attempt commits successfully.
- `sql-inbox-outbox`: SQL transactional commit behavior must include saga state changes in addition to inbox records and outbox operations.
- `sql-saga-persistence`: SQL saga persistence must participate in the active SQL processing transaction during message processing while preserving lifecycle, serialization, and optimistic concurrency behavior.

## Impact

- `src/MiniBus.Core/Sagas`: may need a revised saga persistence boundary, session-aware saga persistence abstraction, or adapter that allows saga mutations to be staged with processing persistence.
- `src/MiniBus.Core/Persistence`: may need to expose saga persistence work through the processing persistence session so providers can commit all durable processing state together.
- `src/MiniBus.AzureFunctions/Processing/Pipeline`: saga invocation and persistence commit ordering may need adjustment so saga state is part of the same unit of work as inbox/outbox state.
- `src/MiniBus.Persistence.Sql`: SQL saga persistence must use the same connection and transaction as SQL inbox/outbox during processing.
- `tests/MiniBus.AzureFunctions.Tests` and `tests/MiniBus.Persistence.Sql.Tests`: add focused unit and SQL integration tests for rollback, duplicate, conflict, timeout scheduling, and commit failure behavior.
- Documentation for sagas and SQL persistence must explain the atomic processing boundary and any behavior differences for non-transactional or custom saga persistence providers.
