# MiniBus

MiniBus is a small Azure-native message-processing framework for .NET applications running on Azure Functions and Azure Service Bus. It takes inspiration from the most useful NServiceBus ideas, but keeps the implementation intentionally compact and explicit.

The goal is to hide repetitive messaging infrastructure concerns while keeping business handlers simple, testable, and independent from Azure SDK and Azure Functions trigger types.

## What It Provides

- Transport-agnostic message contracts for commands, events, and generic messages.
- Handler discovery and invocation through dependency injection.
- Handler-facing `MiniBusContext` for metadata, headers, send, publish, and schedule operations.
- Azure Service Bus transport support for routed send, publish, and scheduled dispatch.
- Azure Functions isolated worker adapter for processing Service Bus trigger messages.
- Header mapping, message identity, correlation, and causation propagation.
- Basic recoverability with immediate retries, delayed retries, and dead-lettering.
- Minimal saga abstractions with explicit correlation and in-memory persistence for tests and samples.
- SQL Server/Azure SQL inbox/outbox persistence with connection-string setup, deterministic outbox replay ids, explicit schema scripts, and caller-provided `DbConnection` factory escape hatches.

## Architecture

Azure Functions are treated as a thin transport adapter. Business handlers should not depend on `ServiceBusReceivedMessage`, `ServiceBusMessageActions`, Functions binding attributes, or Azure SDK transport details.

```text
Azure Function trigger
        |
        v
MiniBus Azure Functions adapter
        |
        v
MiniBus processor
        |
        v
Deserialize message
        |
        v
Invoke handlers and sagas
        |
        v
Persist inbox/outbox state when configured
        |
        v
Dispatch outgoing messages directly or through the outbox
        |
        v
Complete, retry, schedule retry, or dead-letter
```

## Projects

- `src/MiniBus.Core`: message contracts, handler APIs, context, serialization, routing, recoverability, saga abstractions, and persistence abstractions.
- `src/MiniBus.AzureServiceBus`: Azure Service Bus routing, envelope creation, header mapping, dispatch, scheduling, and delayed retry scheduling.
- `src/MiniBus.AzureFunctions`: Azure Functions isolated worker processor and settlement integration.
- `src/MiniBus.Persistence.Sql`: SQL Server/Azure SQL inbox/outbox persistence with connection-string registration, schema script packaging, and a `DbConnection` factory escape hatch.
- `samples/MiniBus.Samples.FunctionApp`: buildable Functions-oriented sample showing MiniBus registration, a Service Bus trigger wrapper, handler code, routing, recoverability, and saga setup.
- `tests/*`: unit tests for core behavior, transport, Functions processing, and SQL persistence components.

## SQL Persistence

`MiniBus.Persistence.Sql` provides the inbox/outbox contracts, schema script, persistence session, outbox store, and dispatcher. The common SQL Server/Azure SQL setup path uses a connection string:

```csharp
services.AddMiniBusSqlPersistence(
    connectionString,
    options =>
    {
        options.DispatcherBatchSize = 100;
        options.OutboxClaimLeaseDuration = TimeSpan.FromMinutes(5);
    });
```

Run the scripts in `src/MiniBus.Persistence.Sql/Schema/` against the target database in filename order before enabling the package. MiniBus ships explicit SQL scripts instead of applying runtime migrations; applications should apply those scripts through their normal database deployment flow. The packaged scripts target the default `MiniBus` schema and table names. If an application configures custom SQL schema or table names, it must adapt the scripts until MiniBus grows an intentional script-generation story.

`MiniBus.Outbox` stores a deterministic outgoing message id for each newly captured operation. If an outbox dispatcher sends a message and crashes before marking the row as dispatched, a later replay uses the same outgoing message id so broker duplicate detection and downstream idempotency can recognize the retry where configured. The `002-outbox-outgoing-message-id.sql` migration backfills existing outbox rows from their row ids because the original capture sequence cannot be reconstructed reliably; drain or manually clean old pending rows before applying it if those legacy rows also require deterministic ids.

Outbox claims use `OutboxClaimLeaseDuration` to recover from crashed dispatchers. A claimed row is invisible to other dispatch cycles until the lease expires, then it can be reclaimed.

Applications that need custom connection ownership can still provide a concrete SQL connection factory:

```csharp
services.AddMiniBusSqlPersistence(options =>
{
    options.ConnectionFactory = () => CreateSqlConnection();
    options.DispatcherBatchSize = 100;
});
```

The normal Azure Functions path lets MiniBus own the SQL transaction for inbox and outbox commits. Applications that need business data and MiniBus persistence in one SQL transaction can use the advanced `SqlMiniBusPersistenceSessionFactory.CreateForTransaction(DbConnection, DbTransaction)` API with an open connection and active transaction. In that mode MiniBus writes its inbox/outbox state inside the caller-owned transaction, but the application remains responsible for commit, rollback, and disposal.

Cleanup is explicit and retention-based:

```csharp
services.AddMiniBusSqlPersistence(
    connectionString,
    options =>
    {
        options.InboxRetention = TimeSpan.FromDays(30);
        options.DispatchedOutboxRetention = TimeSpan.FromDays(7);
        options.FailedOutboxRetention = TimeSpan.FromDays(30);
        options.CleanupBatchSize = 1000;
    });
```

Call `ISqlMiniBusOutboxStore.CleanupAsync` from an application-owned scheduled job or maintenance worker when cleanup should run. Failed outbox records are kept unless `FailedOutboxRetention` is configured.

### SQL Integration Tests

`MiniBus.Persistence.Sql.Tests` includes SQL Server-backed integration tests. They use Testcontainers by default when Docker is available, so developers do not need to provision a database manually.

Prerequisites for the Testcontainers path:

- Docker Desktop or another Docker-compatible engine must be running.
- On Apple Silicon, the engine must be able to run `linux/amd64` containers.
- The tests use the pinned SQL Server image `mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04` and set the container platform to `linux/amd64`.

Run the SQL persistence tests:

```bash
dotnet test tests/MiniBus.Persistence.Sql.Tests/MiniBus.Persistence.Sql.Tests.csproj
```

If Docker is unavailable, the SQL Server-backed tests skip with a clear reason and the normal unit tests still run.

To use an existing SQL Server/Azure SQL database instead of Docker, set `MINIBUS_SQLSERVER_TEST_CONNECTION_STRING` before running the tests:

```bash
export MINIBUS_SQLSERVER_TEST_CONNECTION_STRING='Server=localhost,1433;Database=master;User Id=sa;Password=Your_password123;TrustServerCertificate=True;Encrypt=True'
dotnet test tests/MiniBus.Persistence.Sql.Tests/MiniBus.Persistence.Sql.Tests.csproj --filter FullyQualifiedName~SqlServerIntegrationTests
```

When the environment variable is set, the integration tests use that database and create isolated `MiniBusTest_<guid>` schemas inside it. When it is not set, they try the Testcontainers/Docker path.

## Development Workflow

MiniBus uses OpenSpec-driven development. Project context, feature backlog, active changes, archived changes, and capability specs live under `openspec/`.

Useful commands:

```bash
dotnet test --no-restore
openspec status
openspec list
```

## Status

This is an early framework implementation. The core processing model, Azure Service Bus transport, Azure Functions adapter, recoverability, basic saga support, and SQL inbox/outbox foundation are in place. Production hardening remains planned around SQL saga persistence, observability, and developer tooling.
