# MiniBus.Persistence.Sql

`MiniBus.Persistence.Sql` provides SQL Server and Azure SQL persistence for MiniBus inbox, outbox, outbox dispatch, cleanup, and saga state.

It includes:

- SQL inbox duplicate detection by endpoint and logical message id.
- SQL outbox capture for outgoing `Send`, `Publish`, and `Schedule` operations.
- `SqlMiniBusOutboxDispatcher` for dispatching pending outbox operations.
- Optional hosted-service SQL outbox dispatch for single-process hosts.
- Deterministic outgoing message ids for replay-safe outbox dispatch.
- Claim lease recovery and failure metadata for outbox retry.
- Cleanup APIs for expired inbox and outbox rows.
- SQL-backed `ISagaPersistence` with optimistic concurrency.
- Explicit SQL schema scripts packaged under `contentFiles/any/any/Schema/`.

## Setup

Apply every script in `src/MiniBus.Persistence.Sql/Schema/` to the target database in filename order before enabling the package:

```text
001-inbox-outbox.sql
002-outbox-outgoing-message-id.sql
003-sagas.sql
```

MiniBus ships explicit scripts instead of applying migrations at runtime. The scripts target the default `MiniBus` schema and table names. If an application configures custom SQL schema or table names, adapt the scripts through the application's normal database deployment process.

Register SQL persistence with a SQL Server or Azure SQL connection string:

```csharp
services.AddMiniBusSqlPersistence(
    connectionString,
    options =>
    {
        options.DispatcherBatchSize = 100;
        options.OutboxClaimLeaseDuration = TimeSpan.FromMinutes(5);
        options.SagaTableName = "Sagas";
    });
```

Use the factory overload when the application owns connection creation:

```csharp
services.AddMiniBusSqlPersistence(options =>
{
    options.ConnectionFactory = () => CreateSqlConnection();
    options.DispatcherBatchSize = 100;
});
```

## Inbox and outbox behavior

When SQL persistence is enabled, MiniBus checks the inbox before invoking handlers. Duplicate messages are completed or skipped without invoking handlers again. Successful processing commits the inbox record and captured outbox operations before the Azure Functions adapter completes the received Service Bus message.

Outgoing operations are at-least-once. The SQL outbox dispatcher claims a bounded batch, dispatches each operation through the configured transport, and marks successful rows dispatched. If a process exits after the broker accepts a message but before the row is marked dispatched, the operation can be sent again. Use deterministic outgoing message ids, broker duplicate detection where configured, and idempotent receivers.

Outbox dispatch is separate from handler execution. Handlers finish by committing inbox and outbox state durably; transport dispatch happens later through either an application-owned manual drain or the optional hosted drain. That separation is what lets the SQL outbox recover after process crashes without holding broker calls inside the SQL transaction.

Manual dispatch remains the default and is the right fit for dedicated dispatcher processes, timer-triggered drains, tests, and hosts that need custom scheduling:

```csharp
var dispatcher = serviceProvider.GetRequiredService<SqlMiniBusOutboxDispatcher>();
var dispatched = await dispatcher.DispatchPendingAsync(cancellationToken);
```

`DispatchPendingAsync(CancellationToken)` claims and dispatches one SQL batch. Dispatcher workers that want a bounded drain can use the named multi-batch API:

```csharp
var dispatched = await dispatcher.DispatchPendingBatchesAsync(maxBatches: 10, cancellationToken);
```

### Choosing an outbox dispatch host

All dispatch hosting models use the same durable primitive: `SqlMiniBusOutboxDispatcher` claims committed SQL rows, dispatches through the configured transport, and records success or failure metadata. Choose the host based on operational ownership and latency needs:

| Dispatch host | Wake-up behavior | Recommended use |
| --- | --- | --- |
| Manual command or maintenance job | Runs only when invoked | Tests, local troubleshooting, scripted drains, custom schedulers |
| Same process hosted service | Best-effort in-process wake-up after successful MiniBus-owned commits, plus polling | Single-process hosts that intentionally want low-latency automatic draining in the processing host |
| Timer-triggered Azure Function in the processing Function App | Timer cadence | Small Azure Functions deployments that want one host boundary and explicit trigger scheduling |
| Separate timer-triggered dispatcher Function App | Timer cadence | Production-style Azure Functions deployments where processing and outbox draining should be deployed, scaled, observed, and restarted independently |
| Separate worker or hosted service process | Poll interval unless the app adds its own signal | Dedicated dispatcher ownership outside Azure Functions |

The same-process hosted service can feel nearly instant because MiniBus wakes the local dispatcher after the SQL transaction commits. That wake-up is a process-local hint, not part of the SQL transaction and not a correctness mechanism. A separate dispatcher app does not receive that built-in in-memory wake-up from the handler app; it discovers committed rows through its timer or polling cadence. Both shapes remain correct because SQL claims and claim-lease recovery coordinate the real work.

Avoid running multiple scheduler types for the same outbox unless that is a deliberate scale-out or recovery choice. Multiple dispatchers can coexist because SQL claims coordinate rows, but outgoing delivery remains at-least-once after crash windows.

Single-process hosts can opt into automatic hosted dispatch after SQL persistence registration:

```csharp
services
    .AddMiniBusSqlPersistence(connectionString)
    .AddMiniBusSqlHostedOutboxDispatch(options =>
    {
        options.PollInterval = TimeSpan.FromSeconds(5);
        options.MaxBatchesPerCycle = 10;
        options.FailureBackoff = TimeSpan.FromSeconds(30);
        options.DrainOnStartup = true;
    });
```

Hosted dispatch validates that `PollInterval`, `MaxBatchesPerCycle`, and `FailureBackoff` are greater than zero. `DrainOnStartup` controls whether the background service runs an immediate drain cycle before waiting for the first poll interval. The mutable options object is used only during registration; the hosted dispatcher receives an immutable settings snapshot. After successful MiniBus-owned SQL commits with outbox work, the hosted service receives a best-effort in-process wake-up; correctness still depends on polling and SQL claim-lease recovery, so application-owned transactions continue to be discovered through polling.

Advanced hosts can replace `ISqlMiniBusOutboxDispatchSignal` before calling `AddMiniBusSqlHostedOutboxDispatch` when they need custom in-process wake-up coordination. Custom signal implementations must treat `Wake` as best-effort and propagate cancellation from `WaitAsync`.

Outbox claims use `OutboxClaimLeaseDuration`; abandoned claims become eligible for later dispatch after the lease expires. Cleanup is explicit through `ISqlMiniBusOutboxStore.CleanupAsync` and retention options.

## SQL saga persistence

SQL persistence registers SQL-backed `ISagaPersistence` when no custom saga persistence has already been registered. Saga data is stored by saga data type and correlation id using the configured MiniBus serializer. Saves and completions use SQL Server rowversion metadata for optimistic concurrency; stale updates fail with `SagaPersistenceException` and flow through normal message recoverability.

Keep saga data focused on workflow state. Data type renames and shape migrations are application-owned because stored saga rows are keyed by data type identity.

## Verification

`MiniBus.Persistence.Sql.Tests` can run SQL Server-backed integration tests through Testcontainers when Docker is available, or against an external SQL Server/Azure SQL database when `MINIBUS_SQLSERVER_TEST_CONNECTION_STRING` is set. If neither path is available, SQL-backed integration tests skip with a clear reason.

Publishing packages to NuGet is not part of the current repository workflow.
