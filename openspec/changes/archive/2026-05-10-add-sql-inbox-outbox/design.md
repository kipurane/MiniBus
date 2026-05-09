## Context

MiniBus currently provides transport-independent message contracts, handler invocation, serialization, routing, basic saga abstractions, Azure Service Bus dispatch, Azure Functions processing, and basic recoverability. The processor can retry, schedule delayed retry copies, dead-letter exhausted failures, and dispatch outgoing operations through the Azure Service Bus transport.

The remaining production reliability gap is durable coordination. Without an inbox, the same incoming message can invoke business handlers more than once after host restarts or settlement uncertainty. Without an outbox, outgoing messages are sent directly during processing and cannot be committed atomically with handler state or replayed after a transient dispatch failure. SQL Server / Azure SQL is already the planned production persistence option, so this change introduces the first durable persistence package around inbox and outbox behavior.

## Goals / Non-Goals

**Goals:**
- Add SQL-backed inbox storage that records processed incoming message identities by endpoint and avoids re-running handlers for duplicates.
- Add SQL-backed outbox storage that captures outgoing `Send`, `Publish`, and `Schedule` operations during handler execution.
- Commit inbox and outbox records in one SQL transaction before the Azure Functions adapter completes the incoming Service Bus message.
- Provide an outbox dispatcher that loads pending operations, dispatches them through the configured transport, and marks them dispatched.
- Keep SQL persistence opt-in so the existing direct-dispatch path remains available for simple applications and tests.
- Provide schema creation scripts or migrations that are explicit and reviewable.

**Non-Goals:**
- Full application data transaction enlistment beyond MiniBus inbox/outbox tables.
- Distributed transactions between SQL Server and Azure Service Bus.
- First-class SQL Server/Azure SQL client packaging; for this slice, applications provide the concrete `DbConnection` factory.
- SQL saga persistence; saga storage remains a separate planned persistence capability.
- Azure Storage persistence, blob claim-check, or audit storage.
- A production scheduler/host for outbox dispatch beyond a callable service and documented hosting pattern.

## Decisions

### Use a dedicated SQL persistence package

Create `MiniBus.Persistence.Sql` instead of adding SQL types to `MiniBus.Core` or the Azure Functions adapter. Core should define small abstractions for inbox and outbox behavior, while the SQL package owns ADO.NET-style persistence details, SQL Server-shaped schema, and registration.

Alternative considered: keep persistence entirely in the Azure Functions adapter. That would make the first implementation shorter, but it would couple reliability semantics to one host and make later transport or host adapters harder to reuse.

### Defer first-class SQL Server client packaging

The first implementation should accept a caller-provided `DbConnection` factory instead of taking a direct `Microsoft.Data.SqlClient` dependency. This is sufficient for the current reliability slice because the package can exercise inbox/outbox behavior against provider-neutral ADO.NET abstractions while leaving the application in control of the concrete SQL Server/Azure SQL client package.

Alternative considered: reference `Microsoft.Data.SqlClient` immediately and expose only a connection string option. That is a better turnkey developer experience, but it should land together with SQL Server/Azure SQL integration tests, versioned packaging decisions, and clearer migration guidance.

### Model the inbox by endpoint and original message id

The inbox key should include endpoint name and a stable message id, preferring `MiniBus.OriginalMessageId` when present and falling back to the received message id. This matches recoverability behavior where delayed retry copies receive new transport ids but still represent the same logical incoming message.

Alternative considered: use only Service Bus `MessageId`. That is simpler, but delayed retry copies and manual resubmissions can change transport identity, weakening deduplication across retry flows.

### Capture outgoing work as serialized outbox operations

When SQL outbox is enabled, `MiniBusContext.Send`, `Publish`, and `Schedule` should append outgoing operations to the current processing context instead of immediately invoking transport dispatch. The commit step persists serialized body, message type, operation kind, destination/routing metadata where needed, headers, due time, and correlation metadata.

Alternative considered: persist fully formed Azure Service Bus messages. That would make dispatch easier for this transport, but it would leak transport details into the persistence model and make future transports harder.

### Dispatch pending outbox operations through a claim-and-mark workflow

The dispatcher should claim a bounded batch of pending rows, dispatch each operation through the existing MiniBus dispatch abstraction, then mark successful rows dispatched. Failed rows remain retryable with attempt metadata and last error details.

Alternative considered: delete rows after dispatch. Keeping dispatched metadata for a configurable retention window is more useful for diagnostics and avoids losing operational history immediately after success.

### Keep incoming settlement after SQL commit

For settlement-enabled Azure Functions processing, the processor should complete the received Service Bus message only after handler execution and the SQL transaction commit succeed. If the commit fails, the processor should propagate the failure to the host so the incoming message can be retried by existing host or MiniBus recoverability behavior.

Alternative considered: complete before commit to reduce lock duration. That risks message loss if the process exits before persistence completes.

## Risks / Trade-offs

- Duplicate outgoing dispatch can still occur if the process crashes after a Service Bus send succeeds but before the outbox row is marked dispatched. Mitigation: document at-least-once dispatch semantics and rely on receiver idempotency/inbox behavior.
- Long handler execution can hold SQL resources if the implementation keeps a transaction open around business work. Mitigation: keep the MiniBus transaction focused on inbox/outbox persistence and avoid requiring application data enlistment in this phase.
- Inbox rows can grow without bound. Mitigation: include processed timestamp and retention guidance so cleanup can be implemented safely.
- Outbox dispatch ordering can conflict with parallel dispatch throughput. Mitigation: provide deterministic ordering within a batch but do not guarantee global ordering across dispatcher instances.
- Schema evolution can become difficult if scripts are informal. Mitigation: version schema artifacts and include tests that assert required tables, indexes, and columns.

## Migration Plan

1. Add the SQL persistence package and core abstractions without changing default behavior.
2. Add schema creation scripts for the initial inbox and outbox tables.
3. Add opt-in DI configuration, for example `AddMiniBusSqlPersistence(...)`, and document required `DbConnection` factory configuration.
4. Integrate the Azure Functions processor with SQL persistence only when the persistence services are registered/enabled.
5. Add tests for duplicate detection, outbox capture, transactional commit, dispatch, and fallback direct-dispatch behavior.
6. Rollback by disabling SQL persistence registration; existing direct dispatch and recoverability behavior remains available.

## Open Questions

- Should the first SQL implementation use raw ADO.NET to keep dependencies minimal, or Dapper to reduce mapping boilerplate?
- Should schema management ship as SQL scripts only, or also as a small migrator service?
- What default retention period should be recommended for processed inbox rows and dispatched outbox rows?
- Future feature: add first-class SQL Server/Azure SQL support with `Microsoft.Data.SqlClient`, connection-string registration, and SQL Server-backed integration tests.
