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
- SQL inbox/outbox persistence foundation using caller-provided `DbConnection` factories.

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
    });
```

Run `src/MiniBus.Persistence.Sql/Schema/001-inbox-outbox.sql` against the target database before enabling the package. The script creates `MiniBus.Inbox` for processed-message ids and `MiniBus.Outbox` for pending outgoing send, publish, and schedule operations.

Applications that need custom connection ownership can still provide a concrete SQL connection factory:

```csharp
services.AddMiniBusSqlPersistence(options =>
{
    options.ConnectionFactory = () => CreateSqlConnection();
    options.DispatcherBatchSize = 100;
});
```

SQL Server-backed integration tests are opt-in. Set `MINIBUS_SQLSERVER_TEST_CONNECTION_STRING` before running the SQL persistence tests to verify the packaged schema and persistence behavior against SQL Server/Azure SQL.

## Development Workflow

MiniBus uses OpenSpec-driven development. Project context, feature backlog, active changes, archived changes, and capability specs live under `openspec/`.

Useful commands:

```bash
dotnet test --no-restore
openspec status
openspec list
```

## Status

This is an early framework implementation. The core processing model, Azure Service Bus transport, Azure Functions adapter, recoverability, basic saga support, and SQL inbox/outbox foundation are in place. Production hardening remains planned around SQL Server integration tests, deterministic outbox message ids, cleanup policies, SQL saga persistence, observability, and developer tooling.
