# MiniBus.Persistence.Sql

`MiniBus.Persistence.Sql` provides SQL Server and Azure SQL persistence for MiniBus inbox, outbox, outbox dispatch, cleanup, and saga state.

It includes:

- SQL inbox duplicate detection by endpoint and logical message id.
- SQL outbox capture for outgoing `Send`, `Publish`, and `Schedule` operations.
- `SqlMiniBusOutboxDispatcher` for dispatching pending outbox operations.
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

```csharp
var dispatcher = serviceProvider.GetRequiredService<SqlMiniBusOutboxDispatcher>();
var dispatched = await dispatcher.DispatchPendingAsync(cancellationToken);
```

Outbox claims use `OutboxClaimLeaseDuration`; abandoned claims become eligible for later dispatch after the lease expires. Cleanup is explicit through `ISqlMiniBusOutboxStore.CleanupAsync` and retention options.

## SQL saga persistence

SQL persistence registers SQL-backed `ISagaPersistence` when no custom saga persistence has already been registered. Saga data is stored by saga data type and correlation id using the configured MiniBus serializer. Saves and completions use SQL Server rowversion metadata for optimistic concurrency; stale updates fail with `SagaPersistenceException` and flow through normal message recoverability.

Keep saga data focused on workflow state. Data type renames and shape migrations are application-owned because stored saga rows are keyed by data type identity.

## Verification

`MiniBus.Persistence.Sql.Tests` can run SQL Server-backed integration tests through Testcontainers when Docker is available, or against an external SQL Server/Azure SQL database when `MINIBUS_SQLSERVER_TEST_CONNECTION_STRING` is set. If neither path is available, SQL-backed integration tests skip with a clear reason.

Publishing packages to NuGet is not part of the current repository workflow.
