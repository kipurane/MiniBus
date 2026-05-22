# MiniBus

MiniBus is a small Azure-native message-processing framework for .NET applications running on Azure Functions and Azure Service Bus. It mimics common patterns from similar message bus frameworks, but keeps the implementation intentionally compact and explicit.

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
- SQL Server/Azure SQL inbox, outbox, and saga persistence with connection-string setup, deterministic outbox replay ids, explicit schema scripts, and caller-provided `DbConnection` factory escape hatches.
- Azure Blob Storage payload persistence, claim-check/DataBus behavior for large messages, and optional audit blobs.
- Structured logs, OpenTelemetry-friendly processing traces, and provider-neutral processing/outbox metrics.
- `MiniBus.Testing` helpers for direct handler and saga handler unit tests.
- Optional source-generated Azure Functions Service Bus trigger wrappers that delegate to `MiniBusProcessor`.

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
Internal processing pipeline
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

The processor keeps the Azure Functions-facing API small and delegates internal orchestration to ordered pipeline behaviors for metadata adaptation, type resolution, deserialization, persistence, handler and saga invocation, recoverability, and settlement. The pipeline is an internal framework seam; application code still depends on message contracts, handlers, and `MiniBusContext` rather than middleware types.

## Projects

- `src/MiniBus.Core`: message contracts, handler APIs, context, serialization, routing, recoverability, saga abstractions, and persistence abstractions.
- `src/MiniBus.AzureServiceBus`: Azure Service Bus routing, envelope creation, header mapping, dispatch, scheduling, and delayed retry scheduling.
- `src/MiniBus.AzureFunctions`: Azure Functions isolated worker processor and settlement integration.
- `src/MiniBus.Persistence.Sql`: SQL Server/Azure SQL inbox/outbox/saga persistence with connection-string registration, schema script packaging, and a `DbConnection` factory escape hatch.
- `src/MiniBus.Persistence.AzureStorage`: Azure Blob Storage payload persistence, claim-check support, and audit blob writing.
- `src/MiniBus.Testing`: lightweight direct handler and saga handler unit-testing helpers.
- `src/MiniBus.AzureFunctions.SourceGenerators`: optional source generators for thin Azure Functions Service Bus trigger wrappers.
- `src/MiniBus.Analyzers`: optional Roslyn analyzers for common MiniBus configuration, routing, handler, and message contract mistakes.
- `src/MiniBus.Templates`: `dotnet new` starters for the first Azure Functions + Azure Service Bus MiniBus project path.
- `samples/MiniBus.Samples.FunctionApp`: emulator-runnable Billing Functions sample showing MiniBus registration, Service Bus trigger wrappers, handler code, routing, recoverability, saga setup, and an opt-in SQL-backed reliability path.
- `tests/*`: unit, integration, and acceptance tests for core behavior, transport, Functions processing, SQL persistence, Azure Storage persistence, and reference solution composition.

## Golden Path

The first reusable project starter creates a complete Azure Functions isolated-worker host with manual Service Bus trigger code, MiniBus registration, starter contracts, one handler, routes, recoverability defaults, and generated-project notes:

```bash
dotnet new install MiniBus.Templates
dotnet new minibus-functionapp -n Contoso.Orders.FunctionApp
```

MiniBus package publishing is still a local workflow step in this repository. To inspect the template package before publication, pack it and install the generated `.nupkg`:

```bash
dotnet pack src/MiniBus.Templates/MiniBus.Templates.csproj -c Release
dotnet new install artifacts/packages/MiniBus.Templates.0.1.0-preview.1.nupkg
```

The template keeps Azure Service Bus credentials, queues, topics, subscriptions, deployment, and SQL reliability wiring application-owned. The manual setup path below remains useful when you want to assemble the registration yourself or study the underlying pieces.

For an early Azure Functions + Azure Service Bus application, start with the package set that matches the runtime you want:

```bash
dotnet add package MiniBus.Core
dotnet add package MiniBus.AzureServiceBus
dotnet add package MiniBus.AzureFunctions
dotnet add package MiniBus.Persistence.Sql
dotnet add package MiniBus.Persistence.AzureStorage
dotnet add package MiniBus.Testing
# Optional: generates thin Azure Functions Service Bus trigger wrappers
dotnet add package MiniBus.AzureFunctions.SourceGenerators
# Optional: compile-time guidance for common MiniBus mistakes
dotnet add package MiniBus.Analyzers
```

At the moment these packages are prepared for local pack verification; publishing to NuGet is still a project workflow step, not something this repository does automatically.

1. Define message contracts with `ICommand`, `IEvent`, or `IMessage`.
2. Implement handlers with `IHandleMessages<TMessage>` and depend on `MiniBusContext`, not Azure SDK or Functions trigger types.
3. Register `AddMiniBusAzureFunctions` with endpoint, recoverability, and saga options.
4. Register Azure Service Bus routes, `AzureServiceBusMessageFactory`, `AzureServiceBusTransportDispatcher`, `ServiceBusClient`, `IAzureServiceBusSender`, and delayed retry scheduling.
5. Add manual Azure Functions wrappers or opt into generated wrappers with assembly-level trigger declarations.
6. Apply every script in `src/MiniBus.Persistence.Sql/Schema/` in filename order, then register `AddMiniBusSqlPersistence` if the endpoint needs SQL inbox/outbox/saga persistence.
7. Run `SqlMiniBusOutboxDispatcher.DispatchPendingAsync` from an application-owned scheduled job, timer, or worker when SQL outbox dispatch should drain pending operations.
8. Optionally register Azure Blob payload persistence, claim-check behavior, and audit blob writing.
9. Configure logging, `ActivitySource` listeners, or metrics exporters in the host application. MiniBus emits provider-neutral diagnostics and does not require a specific observability SDK.
10. Unit test handlers and saga handlers directly with `MiniBus.Testing`; use processor, SQL, Azure Storage, or live integration tests only when that level of infrastructure is the thing under test.

Manual Azure Functions wrappers remain supported and easy to debug. Source-generated wrappers are optional developer tooling for queue and topic/subscription triggers. `MiniBus.Analyzers` provides optional compile-time guidance for high-signal MiniBus handler, message contract, routing, Azure Functions setup, and saga configuration mistakes; the first project template includes it by default, while manually assembled applications can opt in. Live Azure Service Bus integration tests, automatic Azure infrastructure provisioning, and package publishing automation are future work.

The Billing sample keeps the first emulator loop lightweight, then shows the SQL-backed reliability increment separately: SQL schema application, opt-in SQL persistence registration for inbox/outbox/saga state, and application-owned outbox draining through `SqlMiniBusOutboxDispatcher`.

## SQL Persistence

`MiniBus.Persistence.Sql` provides the inbox/outbox contracts, SQL saga persistence, schema scripts, persistence session, outbox store, and dispatcher. The common SQL Server/Azure SQL setup path uses a connection string:

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

Run the scripts in `src/MiniBus.Persistence.Sql/Schema/` against the target database in filename order before enabling the package. MiniBus ships explicit SQL scripts instead of applying runtime migrations; applications should apply those scripts through their normal database deployment flow. The packaged scripts target the default `MiniBus` schema and table names. If an application configures custom SQL schema or table names, it must adapt the scripts until MiniBus grows an intentional script-generation story.

When SQL persistence is registered, MiniBus also registers SQL-backed `ISagaPersistence`. Saga data is stored in the configured saga table by saga data type and correlation id, with the serialized saga data, completion flag, completion timestamp, and SQL Server rowversion metadata. Saves and completions use optimistic concurrency; stale updates fail with `SagaPersistenceException` so normal message recoverability can retry or escalate the processing attempt.

SQL saga persistence uses the configured MiniBus serializer for saga data. Keep saga data focused on workflow state rather than large document payloads, and treat data type renames as application-owned data migrations because stored rows are keyed by the saga data type identity.

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

## Azure Storage payload persistence and audit blobs

Azure Storage persistence is opt-in through `MiniBus.Persistence.AzureStorage`. It provides Blob-backed payload storage for opaque payload bytes, claim-check/DataBus storage for large messages, and optional Blob-backed audit writing for processed inbound messages.

```csharp
services.AddMiniBusAzureStoragePersistence(
    connectionString,
    containerName: "minibus-payloads",
    options =>
    {
        options.BlobNamePrefix = "payloads";
        options.PayloadRetention = TimeSpan.FromDays(7);
    });
```

Applications can also register a caller-provided `BlobContainerClient` factory when they need custom Azure SDK client ownership. With the default MiniBus registrations, that delegate is evaluated when the singleton payload store or audit writer is constructed, so the resulting `BlobContainerClient` is normally reused for the lifetime of the service provider rather than recreated on every payload or audit operation. Callers can return a new client if they need custom ownership semantics, but reusing a cached `BlobContainerClient` is usually the better choice when sharing Azure SDK configuration and connection pools across components. The payload store returns MiniBus-owned payload references containing container name, blob name, payload id, length, content type, creation timestamp, and optional expiry timestamp; handlers and message contracts do not need to reference Azure SDK types.

Blob-backed audit writing is registered separately so applications can opt in without changing handler code:

```csharp
services.AddMiniBusAzureBlobAudit(
    connectionString,
    auditContainerName: "minibus-audits",
    options =>
    {
        options.AuditBlobNamePrefix = "audits";
        options.AuditRetention = TimeSpan.FromDays(30);
    });
```

Audit records are written before final completion or dead-letter settlement and include message identity, correlation metadata, headers, outcome, retry/dead-letter metadata, and inline body or claim-check reference context. Audit write failures are treated as processing failures when auditing is enabled, so MiniBus does not silently complete a message that was expected to be audited.

`MiniBus.Persistence.AzureStorage.Tests` runs Blob Storage integration coverage through Testcontainers-backed Azurite when Docker is available. Set `MINIBUS_AZURE_STORAGE_TEST_CONNECTION_STRING` to run the same integration coverage against an external Azure Storage account instead of starting Azurite. If neither Docker nor a live connection string is available, those integration tests skip with a clear reason.

If Docker is unavailable, the SQL Server-backed tests skip with a clear reason and the normal unit tests still run.

To use an existing SQL Server/Azure SQL database instead of Docker, set `MINIBUS_SQLSERVER_TEST_CONNECTION_STRING` before running the tests:

```bash
export MINIBUS_SQLSERVER_TEST_CONNECTION_STRING='Server=localhost,1433;Database=master;User Id=sa;Password=Your_password123;TrustServerCertificate=True;Encrypt=True'
dotnet test tests/MiniBus.Persistence.Sql.Tests/MiniBus.Persistence.Sql.Tests.csproj --filter FullyQualifiedName~SqlServerIntegrationTests
```

When the environment variable is set, the integration tests use that database and create isolated `MiniBusTest_<guid>` schemas inside it. When it is not set, they try the Testcontainers/Docker path.

### Reference Solution Acceptance Tests

`MiniBus.AcceptanceTests` provides a small high-level canary layer above the unit, adapter, transport, SQL, and Azure Storage suites.

Tier 1 acceptance tests are always-on and infrastructure-free. They build a real service provider from sample-style MiniBus registration, use recording transport and settlement doubles, and process a realistic billing workflow without Docker, live Azure Service Bus, or a real Azure Functions host.

Tier 2 acceptance tests verify one SQL-backed reference workflow. They use the same SQL Server Testcontainers path as the SQL persistence integration tests, or `MINIBUS_SQLSERVER_TEST_CONNECTION_STRING` when an external SQL Server/Azure SQL database should be used. If neither Docker nor the external connection string is available, SQL-backed acceptance tests skip with a clear reason.

Run the acceptance tests:

```bash
dotnet test tests/MiniBus.AcceptanceTests/MiniBus.AcceptanceTests.csproj
```

## Development Workflow

MiniBus uses OpenSpec-driven development. Project context, feature backlog, active changes, archived changes, and capability specs live under `openspec/`.

Useful commands:

```bash
dotnet test --no-restore
dotnet pack MiniBus.sln -c Release
./eng/verify-templates.sh
openspec status
openspec list
```

## Status

This is an early framework implementation with the core processing model, Azure Service Bus transport, Azure Functions adapter, recoverability, saga support, SQL inbox/outbox/saga persistence, Azure Storage claim-check/audit support, observability, testing helpers, source-generated Functions wrappers, Roslyn analyzers, the first project template, the emulator-runnable Billing sample with an opt-in SQL-backed reliability path, and reference acceptance coverage in place. The next production-readiness work is developer tooling and distribution polish: fuller samples, live Azure integration coverage, and publishing automation.
