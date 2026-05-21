# MiniBus.Samples.FunctionApp

This sample is a minimal buildable Azure Functions-oriented project that shows the stable MiniBus setup path:

- manual Service Bus trigger wrapper
- optional source-generated Service Bus trigger wrapper declarations
- `AddMiniBusAzureFunctions` registration
- `SystemTextJsonMessageSerializer` registration
- regular handler registration
- basic saga registration that reacts to `InvoiceCreated` and requests a timeout
- Azure Service Bus route and dispatcher registration
- recoverability settings

## Build

```bash
dotnet build samples/MiniBus.Samples.FunctionApp/MiniBus.Samples.FunctionApp.csproj
```

The sample is intended to compile without provisioning Azure resources.

## Configuration Notes

`BillingInputFunction` expects a Service Bus trigger connection named `ServiceBus` and an input queue named `billing-queue` when run as a real Function App. `BillingEventsFunction` shows the matching event-processing wrapper for the `domain-events` topic and `billing` subscription used by `InvoiceCreated`.

`Program.ConfigureServices` shows the service registration that a real isolated worker host would call from its startup path. Start from the `minibus-functionapp` project template when you want a complete generated host; this sample stays focused on the reference registration and sample-code shape.

Manual wrappers remain the clearest sample default because the generated source is produced at build time. Applications that prefer generated wrappers can reference `MiniBus.AzureFunctions.SourceGenerators` and declare queue or topic/subscription inputs with assembly attributes:

```csharp
using MiniBus.AzureFunctions.SourceGenerators.Declarations;

[assembly: MiniBusSourceGeneratedServiceBusQueueFunction("BillingInput", "billing-queue", "ServiceBus")]
[assembly: MiniBusSourceGeneratedServiceBusTopicFunction("BillingEvents", "domain-events", "billing", "ServiceBus")]
```

The generated wrappers use the same `MiniBusProcessor.ProcessAsync` path as the manual `BillingInputFunction` and `BillingEventsFunction` classes. Keep manual wrappers when custom trigger code or especially explicit debugging is more useful than generated boilerplate.

The registered `SampleServiceBusSender` is a placeholder that throws if outgoing dispatch is attempted. Replace it with `AzureServiceBusSender` backed by an Azure `ServiceBusClient` when connecting the sample to a real Service Bus namespace.

`BillingSaga` shows the saga timeout pattern. The timeout is an ordinary MiniBus message implementing `ISagaTimeout`; the saga requests it with `RequestTimeout`, correlates it back with `Correlate<InvoicePaymentTimeout>`, and the transport schedules it through the `billing-timeouts` route. This version uses Azure Service Bus scheduled messages as the timeout mechanism. MiniBus does not add a SQL timeout table or SQL-managed timeout dispatcher for this path.

SQL inbox/outbox persistence is intentionally not wired into the sample's default build. A real Function App can add it next to the existing MiniBus registration:

```csharp
services.AddMiniBusSqlPersistence(
    sqlConnectionString,
    options =>
    {
        options.DispatcherBatchSize = 100;
        options.OutboxClaimLeaseDuration = TimeSpan.FromMinutes(5);
    });
```

Apply the scripts in `src/MiniBus.Persistence.Sql/Schema/` to the target SQL Server/Azure SQL database in filename order before enabling SQL persistence. MiniBus does not apply runtime migrations.

The default Functions path lets MiniBus own the inbox/outbox transaction. Applications that need to commit business data and MiniBus persistence state in the same SQL transaction can use `SqlMiniBusPersistenceSessionFactory.CreateForTransaction` from their own application service, but that is an advanced path rather than the sample default.

Applications that need custom connection ownership can use the existing `DbConnection` factory option instead. Cleanup is also application-scheduled: configure retention options such as `InboxRetention`, `DispatchedOutboxRetention`, and `CleanupBatchSize`, then call `ISqlMiniBusOutboxStore.CleanupAsync` from a maintenance job.

The SQL persistence integration tests use Testcontainers when Docker is available and fall back to `MINIBUS_SQLSERVER_TEST_CONNECTION_STRING` when an external SQL Server/Azure SQL database should be used. On Apple Silicon, Docker Desktop must be able to run `linux/amd64` SQL Server containers.
