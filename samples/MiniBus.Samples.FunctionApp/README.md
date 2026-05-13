# MiniBus.Samples.FunctionApp

This sample is a minimal buildable Azure Functions-oriented project that shows the stable MiniBus setup path:

- manual Service Bus trigger wrapper
- `AddMiniBusAzureFunctions` registration
- `SystemTextJsonMessageSerializer` registration
- regular handler registration
- basic saga registration that reacts to `InvoiceCreated`
- Azure Service Bus route and dispatcher registration
- recoverability settings

## Build

```bash
dotnet build samples/MiniBus.Samples.FunctionApp/MiniBus.Samples.FunctionApp.csproj
```

The sample is intended to compile without provisioning Azure resources.

## Configuration Notes

`BillingInputFunction` expects a Service Bus trigger connection named `ServiceBus` and an input queue named `billing-queue` when run as a real Function App. `BillingEventsFunction` shows the matching event-processing wrapper for the `domain-events` route used by `InvoiceCreated`.

`Program.ConfigureServices` shows the service registration that a real isolated worker host would call from its startup path. The sample intentionally avoids owning the full Functions host executable until the project has a reusable host template.

The registered `SampleServiceBusSender` is a placeholder that throws if outgoing dispatch is attempted. Replace it with `AzureServiceBusSender` backed by an Azure `ServiceBusClient` when connecting the sample to a real Service Bus namespace.

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
