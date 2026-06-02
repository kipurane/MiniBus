# MiniBus.AzureFunctions

`MiniBus.AzureFunctions` provides Azure Functions isolated worker integration for processing Azure Service Bus trigger messages through MiniBus.

Manual Azure Functions wrappers remain a supported integration model. They keep static trigger declarations in the function app and delegate processing to `MiniBusProcessor`. Applications can also opt into source-generated wrappers through `MiniBus.AzureFunctions.SourceGenerators` when they want MiniBus to generate the repetitive wrapper class.

```csharp
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using MiniBus.AzureFunctions.Processing;

public sealed class BillingInputFunction
{
    private readonly MiniBusProcessor _processor;

    public BillingInputFunction(MiniBusProcessor processor)
    {
        _processor = processor;
    }

    [Function("BillingInput")]
    public Task Run(
        [ServiceBusTrigger("billing-queue", Connection = "ServiceBus")]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions actions,
        CancellationToken cancellationToken)
    {
        return _processor.ProcessAsync(message, actions, cancellationToken);
    }
}
```

Handlers still implement `IHandleMessages<TMessage>` from `MiniBus.Core` and receive only the deserialized message, `MiniBusContext`, and `CancellationToken`.

## Generated wrappers

`MiniBus.AzureFunctions.SourceGenerators` can generate the same thin wrapper shape from assembly-level declarations in the Function App project. The declaration attributes live in the reserved `MiniBus.AzureFunctions.SourceGenerators.Declarations` namespace and require explicit trigger metadata; there are no optional generated type-name overrides in this version.

```csharp
using MiniBus.AzureFunctions.SourceGenerators.Declarations;

[assembly: MiniBusSourceGeneratedServiceBusQueueFunction(
    functionName: "BillingInput",
    queueName: "billing-queue",
    connection: "ServiceBus")]

[assembly: MiniBusSourceGeneratedServiceBusTopicFunction(
    functionName: "BillingEvents",
    topicName: "domain-events",
    subscriptionName: "billing",
    connection: "ServiceBus")]
```

The generated class is added to the consuming Function App assembly, injects `MiniBusProcessor`, and calls `ProcessAsync(ServiceBusReceivedMessage, ServiceBusMessageActions, CancellationToken)`. Manual wrappers and generated wrappers can coexist in the same app. The generator reports diagnostics for empty required values and duplicate generated function names.

Generated wrapper types are emitted into the reserved `MiniBus.AzureFunctions.__Generated` namespace. Application code should not define its own types in either reserved source-generator namespace.

## Registration

Register the Azure Functions adapter with endpoint and recoverability options:

```csharp
services.AddMiniBusAzureFunctions(options =>
{
    options.EndpointName = "Billing";
    options.EnableSagas = true;
    options.Recoverability.ImmediateRetries = 3;
    options.Recoverability.DelayedRetries.Add(TimeSpan.FromSeconds(10));
    options.Recoverability.DelayedRetries.Add(TimeSpan.FromMinutes(1));
    options.Recoverability.DelayedRetries.Add(TimeSpan.FromMinutes(5));
    options.Recoverability.DeadLetterAfterRetriesExhausted = true;
});
```

Immediate retries run inside the same `MiniBusProcessor` invocation. Delayed retries use Azure Service Bus scheduled message copies and preserve MiniBus correlation, original message id, retry, and exception headers.

The adapter is normally paired with Azure Service Bus transport registrations:

```csharp
var routes = new AzureServiceBusTransportRoutes();
routes.MapCommand<CreateInvoice>("billing-queue");
routes.MapCommand<SendInvoiceReceipt>("billing-receipts");
routes.MapEvent<InvoiceCreated>("domain-events");
routes.MapScheduledMessage<InvoicePaymentTimeout>("billing-timeouts");

services.AddSingleton(routes);
services.AddSingleton<AzureServiceBusMessageFactory>();
services.AddSingleton<AzureServiceBusTransportDispatcher>();
services.AddSingleton(_ => new ServiceBusClient(serviceBusConnectionString));
services.AddSingleton<IAzureServiceBusSender, AzureServiceBusSender>();
services.AddSingleton<IAzureServiceBusDelayedRetryScheduler, AzureServiceBusDelayedRetryScheduler>();
```

Use `MiniBus.Persistence.Sql` when the endpoint needs SQL inbox/outbox/saga persistence, and `MiniBus.Persistence.AzureStorage` when it needs Blob-backed claim-check or audit behavior.

For a runnable local Functions reference path, the Billing sample under `samples/MiniBus.Samples.Billing.FunctionApp` and sibling Inventory endpoint under `samples/MiniBus.Samples.Inventory.FunctionApp` include manual queue and topic/subscription wrappers, a repo-owned Azure Service Bus emulator topology, a command seed mode, and emulator-gated acceptance verification. The wrappers stay thin: Service Bus trigger arguments still flow directly into `MiniBusProcessor.ProcessAsync`.

## Structured processing logs

MiniBus emits framework-level processing diagnostics through `Microsoft.Extensions.Logging`. Applications keep control of logging providers and sinks; MiniBus uses the host application's existing logging configuration and does not require a MiniBus-specific provider.

Each processing attempt creates a structured log scope after received headers are mapped. MiniBus log entries use stable property names such as `EndpointName`, `MessageType`, `MessageId`, `CorrelationId`, `CausationId`, `RetryAttempt`, `DelayedRetryAttempt`, `HandlerType`, `SagaType`, `SagaCorrelationId`, `ProcessingOutcome`, `OutboxOperationCount`, and `DeadLetterReason` when those values are available.

Processing logs include attempt start, completion, duplicate inbox skips, immediate retry, delayed retry scheduling, dead-lettering, propagated failure, handler invocation, saga invocation/completion, and outbox commit diagnostics. These logs are provider-neutral and are intended to be usable today while leaving OpenTelemetry tracing and metrics as separate observability features.

## OpenTelemetry processing traces

MiniBus emits provider-neutral processing traces through `System.Diagnostics.ActivitySource`. Applications can export these activities by configuring OpenTelemetry or another `ActivityListener`-based pipeline in the host app; MiniBus does not reference the OpenTelemetry SDK, choose exporters, configure resources, or require tracing to be enabled.

The stable ActivitySource name is `MiniBus.Processing` and the root processing activity name is `MiniBus.Process`. Treat both names as observability contracts when configuring `AddSource("MiniBus.Processing")` or log/trace filters.

Processing activities use Azure messaging and MiniBus-specific tags when values are available:

- `messaging.system`
- `minibus.endpoint`
- `minibus.message_type`
- `minibus.message_id`
- `minibus.correlation_id`
- `minibus.causation_id`
- `minibus.retry_attempt`
- `minibus.delayed_retry_attempt`
- `minibus.handler_type`
- `minibus.saga_type`
- `minibus.saga_correlation_id`
- `minibus.processing_outcome`
- `minibus.outbox_operation_count`
- `minibus.dead_letter_reason`

MiniBus records processing outcomes for completed messages, immediate retries, delayed retry scheduling, dead-lettering, duplicate inbox skips, propagated failures, saga completion, and outbox commits where the current pipeline has that context. Error status and exception tags are set for failed processing paths. When no listener is attached, tracing remains a no-op.

## Processing metrics

MiniBus emits provider-neutral metrics through `System.Diagnostics.Metrics`. Applications can export these instruments by configuring OpenTelemetry or another `MeterListener`-based pipeline in the host app; MiniBus does not reference the OpenTelemetry SDK, choose exporters, configure collectors, create dashboards, or require metrics to be enabled.

The stable processing Meter name is `MiniBus.Processing`. SQL outbox dispatch metrics use the stable Meter name `MiniBus.Persistence.Sql`. Treat Meter names, instrument names, units, and tag names as observability contracts when configuring `AddMeter(...)`, dashboards, or alerts.

Processing instruments:

- `minibus.processing.attempts` with unit `{attempt}`
- `minibus.processing.duration` with unit `s`
- `minibus.processing.retries` with unit `{retry}`
- `minibus.processing.dead_letters` with unit `{message}`
- `minibus.processing.duplicates` with unit `{message}`
- `minibus.processing.failures` with unit `{failure}`
- `minibus.handler.duration` with unit `s`
- `minibus.saga.duration` with unit `s`
- `minibus.saga.completions` with unit `{saga}`

SQL outbox dispatch instruments:

- `minibus.sql_outbox.dispatch.batches` with unit `{batch}`
- `minibus.sql_outbox.dispatch.batch_duration` with unit `s`
- `minibus.sql_outbox.dispatch.operations` with unit `{operation}`
- `minibus.sql_outbox.dispatch.operation_duration` with unit `s`

Metric tags intentionally stay bounded for aggregation. Processing metrics use tags such as `minibus.endpoint`, `minibus.message_type`, `minibus.processing_outcome`, `minibus.retry_kind`, `minibus.handler_type`, `minibus.handler_outcome`, `minibus.saga_type`, and `minibus.saga_outcome`. SQL outbox metrics use `minibus.sql_outbox.dispatch_outcome` and `minibus.outbox_operation_kind` when operation kind is known. Metrics do not tag message id, correlation id, causation id, saga correlation id, SQL row id, outgoing transport message id, exception message, dead-letter description, or message body values.

Minimal saga support uses core saga contracts and explicit correlation mappings:

```csharp
public sealed class BillingSagaData : ISagaData
{
    public Guid Id { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public bool InvoiceCreated { get; set; }
}

public sealed class BillingSaga :
    MiniBusSaga<BillingSagaData>,
    IHandleSagaMessages<CreateInvoice>
{
    public override void ConfigureHowToFindSaga(SagaMapper<BillingSagaData> mapper)
    {
        mapper.StartsWith<CreateInvoice>(message => message.InvoiceId);
    }

    public Task Handle(CreateInvoice message, MiniBusContext context, CancellationToken cancellationToken)
    {
        Data.InvoiceCreated = true;
        MarkAsComplete();
        return Task.CompletedTask;
    }
}
```

Register saga mappings explicitly during startup:

```csharp
var sagaRegistry = new SagaRegistry();
sagaRegistry.Register<BillingSaga, BillingSagaData>();

services.AddSingleton(sagaRegistry);
services.AddSingleton<ISagaPersistence, InMemorySagaPersistence>();
services.AddSingleton<SagaInvoker>();
```

`AddMiniBusAzureFunctions` does not register `SagaRegistry` or `SagaInvoker` by default; saga processing is opt-in through `MiniBusProcessorOptions.EnableSagas`. It registers an `UnconfiguredSagaPersistence` placeholder so production apps must choose a real saga store explicitly. `InMemorySagaPersistence` is intended for tests and samples. Production SQL saga persistence is available from `MiniBus.Persistence.Sql`.

## SQL inbox and outbox persistence

SQL inbox/outbox persistence is opt-in through `MiniBus.Persistence.Sql`. The common SQL Server/Azure SQL setup path uses a connection string:

```csharp
services.AddMiniBusAzureFunctions(options =>
{
    options.EndpointName = "Billing";
});

services.AddMiniBusSqlPersistence(
    connectionString,
    options =>
    {
        options.DispatcherBatchSize = 100;
    });
```

Use the `DbConnection` factory overload when the application needs custom connection ownership:

```csharp
services.AddMiniBusSqlPersistence(options =>
{
    options.ConnectionFactory = () => CreateSqlConnection();
    options.DispatcherBatchSize = 100;
});
```

Run every script in `src/MiniBus.Persistence.Sql/Schema/` in filename order before enabling the package. The scripts create and evolve the default `MiniBus` SQL objects for inbox, outbox, deterministic outgoing message ids, and SQL saga state. If the application configures custom SQL schema or table names, adapt the packaged scripts through the application's normal database deployment process.

When SQL persistence is registered, `MiniBusProcessor` checks the inbox before invoking handlers or sagas. Duplicate messages are completed without invoking handlers or sagas again. Successful processing commits the inbox record, captured outbox operations, and any saga state changes before completing the received Service Bus message. If the SQL commit fails, none of those changes from the attempt are durable, the message is not completed, and the failure is propagated to the host.

Outgoing operations are at-least-once. The SQL outbox dispatcher claims a bounded batch of pending operations, dispatches them through the configured transport, then marks successful rows dispatched. If a process exits after Service Bus accepts a message but before the row is marked dispatched, the operation can be sent again; receivers should keep idempotent handlers and inbox persistence enabled.

```csharp
var dispatcher = serviceProvider.GetRequiredService<SqlMiniBusOutboxDispatcher>();
await dispatcher.DispatchPendingAsync(cancellationToken);
```

`DispatchPendingAsync(CancellationToken)` dispatches one SQL batch. Use `DispatchPendingBatchesAsync(maxBatches, cancellationToken)` when a dispatcher worker should drain several batches while still keeping the call bounded.

Outbox rows use deterministic outgoing message ids for replay-safe dispatch. Claimed rows become eligible for later dispatch after `OutboxClaimLeaseDuration` expires. SQL cleanup is explicit through application-owned maintenance code.

When SQL persistence is registered and no custom saga persistence has already been registered, it also provides SQL-backed `ISagaPersistence`. Saga rows store serialized saga data, completion state, timestamps, and SQL Server rowversion metadata. Saves and completions use optimistic concurrency; stale updates fail with `SagaPersistenceException` and flow through normal recoverability. During SQL-backed processing, saga persistence uses the active SQL processing session so saga state, inbox state, and outbox operations commit or roll back together. Standalone SQL saga persistence remains available for direct lifecycle access, but it is separate from the atomic message-processing path.

`MiniBus.Persistence.Sql.Tests` runs SQL Server-backed integration coverage through Testcontainers when Docker is available. The test fixture uses a pinned SQL Server 2022 Linux container image and requests `linux/amd64`, which lets Apple Silicon machines run it through Docker Desktop's amd64 emulation. If Docker is unavailable, those tests skip with a clear reason and the normal unit test run still passes. Set `MINIBUS_SQLSERVER_TEST_CONNECTION_STRING` to run the same integration coverage against an external SQL Server/Azure SQL database instead of starting a container.

## Azure Storage payload persistence and claim-check

`MiniBus.Persistence.AzureStorage` provides Blob-backed payload storage for opaque MiniBus payload bytes. Applications can use it directly through the payload store, or enable optional claim-check/DataBus behavior so large outgoing `Send`, `Publish`, and `Schedule` bodies are stored in Blob Storage and replaced with compact Service Bus claim-check messages.

```csharp
services.AddMiniBusAzureStoragePersistence(
    connectionString,
    containerName: "minibus-payloads",
    options =>
    {
        options.BlobNamePrefix = "payloads";
        options.PayloadRetention = TimeSpan.FromDays(7);
    })
    .AddMiniBusAzureBlobClaimCheck(payloadThresholdBytes: 128 * 1024);
```

Claim-check behavior is opt-in. Messages whose serialized body is at or below the threshold stay inline. Messages above the threshold are written through the configured Blob payload store, sent with MiniBus claim-check headers, and resolved by `MiniBusProcessor` before deserialization so handlers and sagas still receive the original message contract.

Use the options overload to provide a custom `BlobContainerClient` factory when an application owns Azure SDK client configuration. With the default MiniBus registrations, that delegate is evaluated when the singleton payload store or audit writer is constructed, so the resulting `BlobContainerClient` is normally reused for the lifetime of the service provider rather than recreated for each payload or audit operation. Callers can return a new client if they need custom ownership semantics, but reusing a cached `BlobContainerClient` is usually the better choice when sharing Azure SDK configuration and connection pools across components. The payload store returns MiniBus-owned references and keeps Azure SDK types out of handlers, message contracts, saga data, and handler-facing APIs.

Configure Blob payload retention to exceed the longest expected processing window, including delayed retries, scheduled delivery, and SQL outbox replay. Disabling claim-check stops new outgoing messages from using Blob payload storage, but already queued claim-check messages still require receive-side payload resolution until they are processed or dead-lettered.

## Azure Storage audit blobs

Blob-backed audit writing is opt-in and records processed inbound messages before final completion or dead-letter settlement. Audit records include message identity, correlation metadata, headers, outcome, retry/dead-letter metadata, and inline body or claim-check reference context while keeping handlers independent from Azure SDK types.

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

Audit write failures are fail-closed: if auditing is enabled and the audit blob cannot be written, MiniBus treats that as a processing failure and does not complete or dead-letter the received message as if auditing had succeeded. Claim-checked messages audit claim-check metadata by default rather than duplicating large resolved payload bytes.

`MiniBus.Persistence.AzureStorage.Tests` runs Blob Storage integration coverage through Testcontainers-backed Azurite when Docker is available. Set `MINIBUS_AZURE_STORAGE_TEST_CONNECTION_STRING` to run the same tests against a live Azure Storage account.
