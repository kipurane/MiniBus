## Why

MiniBus can already process, retry, settle, and dispatch messages, but business state changes and outgoing messages are still not durably coordinated. SQL-backed inbox and outbox support is the next reliability step because it enables idempotent processing and stores outgoing operations before dispatch, which is required for production-grade Azure Functions workloads.

## What Changes

- Add a new SQL persistence package for inbox and outbox storage against SQL Server / Azure SQL.
- Add core persistence abstractions for inbox deduplication, outbox capture, transactional persistence, and outbox dispatch.
- Allow Azure Functions processing to use SQL persistence so successful handler work can commit inbox and outbox records before the incoming message is completed.
- Add a background or externally callable outbox dispatcher that sends persisted outgoing operations through the configured transport and marks them dispatched.
- Document setup, schema creation, and operational expectations for SQL-backed reliability.
- No breaking changes to existing in-memory/direct-dispatch behavior; SQL persistence is opt-in.

## Capabilities

### New Capabilities
- `sql-inbox-outbox`: SQL Server / Azure SQL persistence for processed-message deduplication, durable outgoing operation capture, transactional commit, and outbox dispatch.

### Modified Capabilities
- `azure-functions-adapter`: Processing can opt into SQL-backed inbox/outbox behavior before completing the received Service Bus message.
- `azure-servicebus-transport`: Transport dispatch is used by the outbox dispatcher for persisted outgoing operations.

## Impact

- New `src/MiniBus.Persistence.Sql` package and `tests/MiniBus.Persistence.Sql.Tests` test project.
- New SQL schema objects for inbox records, outbox records, dispatch state, and optimistic concurrency metadata.
- New DI/configuration APIs for enabling SQL persistence and outbox dispatch.
- Azure Functions processor integration points for inbox checks, transactional outbox capture, duplicate handling, and post-commit settlement.
- Azure Service Bus transport integration for dispatching persisted outbox messages.
