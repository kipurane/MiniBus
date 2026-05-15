# MiniBus.AzureFunctions

Manual Azure Functions isolated worker wrappers keep trigger declarations in the function app and delegate processing to `MiniBusProcessor`.

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

Recoverability is configured with the Azure Functions adapter registration:

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

`AddMiniBusAzureFunctions` does not register `SagaRegistry` or `SagaInvoker` by default; saga processing is opt-in through `MiniBusProcessorOptions.EnableSagas`. It registers an `UnconfiguredSagaPersistence` placeholder so production apps must choose a real saga store explicitly. `InMemorySagaPersistence` is intended for tests and samples. Production SQL saga persistence is deferred.

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

Run `src/MiniBus.Persistence.Sql/Schema/001-inbox-outbox.sql` before enabling the package. The schema creates `MiniBus.Inbox` for processed-message ids and `MiniBus.Outbox` for pending outgoing send, publish, and schedule operations.

When SQL persistence is registered, `MiniBusProcessor` checks the inbox before invoking handlers. Duplicate messages are completed without invoking handlers again. Successful processing commits the inbox record and captured outbox operations before completing the received Service Bus message. If the SQL commit fails, the message is not completed and the failure is propagated to the host.

Outgoing operations are at-least-once. The SQL outbox dispatcher claims a bounded batch of pending operations, dispatches them through the configured transport, then marks successful rows dispatched. If a process exits after Service Bus accepts a message but before the row is marked dispatched, the operation can be sent again; receivers should keep idempotent handlers and inbox persistence enabled.

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

Use the options overload to provide a custom `BlobContainerClient` factory when an application owns Azure SDK client configuration. The payload store returns MiniBus-owned references and keeps Azure SDK types out of handlers, message contracts, saga data, and handler-facing APIs.

Configure Blob payload retention to exceed the longest expected processing window, including delayed retries, scheduled delivery, and SQL outbox replay. Disabling claim-check stops new outgoing messages from using Blob payload storage, but already queued claim-check messages still require receive-side payload resolution until they are processed or dead-lettered.

`MiniBus.Persistence.AzureStorage.Tests` runs Blob Storage integration coverage through Testcontainers-backed Azurite when Docker is available. Set `MINIBUS_AZURE_STORAGE_TEST_CONNECTION_STRING` to run the same tests against a live Azure Storage account.
