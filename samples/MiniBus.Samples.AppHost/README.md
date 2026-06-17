# MiniBus.Samples.AppHost

Aspire AppHost for the local MiniBus reference environment.

The AppHost composes the existing Billing Function App, Inventory Function App, Billing SQL outbox dispatcher Function App, `MiniBus.Tooling.Web`, and the supporting SQL Server, Azurite, and Azure Service Bus emulator containers with one shared set of local settings. It also runs the Billing sample's SQL schema applier as a one-shot startup resource before the SQL-backed hosts start.

## Prerequisites

- Docker Desktop with Linux containers
- Azure Functions Core Tools for the Function App projects
- .NET SDK from `global.json`
- Explicit EULA acceptance when starting the local SQL Server and Azure Service Bus emulator containers

## Run

Stop any manually started compose stack first so the AppHost can own the local emulator and SQL ports:

```bash
cd samples/MiniBus.Samples.Billing.FunctionApp/servicebus-emulator
docker compose down
cd ../../..
```

The AppHost uses Aspire-owned container names and marks them persistent, so stopped compose containers do not block the orchestrated environment. To force a clean rebuild of the AppHost infrastructure, remove the `minibus-aspire-*` containers with Docker after stopping the AppHost.

Run the AppHost from the repository root and accept the local emulator/container licenses:

```bash
ACCEPT_EULA=Y dotnet run --project samples/MiniBus.Samples.AppHost/MiniBus.Samples.AppHost.csproj
```

Wait in the Aspire dashboard until the `sql`, `functions-storage`, and `servicebus-emulator` resources are running. The existing Service Bus emulator topology remains the source of truth:

- `billing-queue`
- `inventory-queue`
- `billing-receipts`
- `domain-events`
- `billing`
- `billing-timeouts`

Wait for the `billing-sql-schema-apply` resource to complete. It runs the same idempotent MiniBus SQL schema scripts used by the manual sample workflow, so existing schema objects are left in place and missing objects/migrations are created before the SQL-backed hosts start.

The AppHost passes these shared settings to the orchestrated projects:

- `ServiceBus`
- `AzureWebJobsStorage`
- `BillingSql`
- `BillingSqlSchema`
- `BillingOutboxDispatchSchedule`
- `BillingOutboxDispatchMaxBatches`
- `MiniBus__Tooling__Sql__ConnectionString`
- `MiniBus__Tooling__Sql__SchemaName`

The Function Apps use distinct local ports so Azure Functions Core Tools can run them side by side:

- Billing Function App: `7071`
- Inventory Function App: `7072`
- Billing outbox dispatcher Function App: `7073`

Seed the Billing workflow from another terminal:

```bash
./samples/MiniBus.Samples.Billing.FunctionApp/seed-local.sh
```

Use the Aspire dashboard to open `MiniBus.Tooling.Web` and inspect inbox, outbox, saga, and timeline state from the same Billing SQL database.

## Configuration

Local defaults live in `appsettings.json` and match the existing sample scripts:

- Service Bus emulator connection string uses `UseDevelopmentEmulator=true`.
- Billing SQL points at the SQL Server port exposed by the AppHost SQL container.
- `billing-sql-schema-apply` runs before the Billing Function App, Billing outbox dispatcher, and `MiniBus.Tooling.Web` start.
- Functions storage uses Azurite through `UseDevelopmentStorage=true`.
- The dispatcher drains every 15 seconds with a maximum of 5 batches.
- `accept-eula` defaults to `N`; set `ACCEPT_EULA=Y` when starting the AppHost.
- The Functions storage connection parameter is `functions-storage-connection-string`; the Azurite container resource is `functions-storage`.

Override values through normal Aspire configuration, user secrets, or environment variables when needed. Do not point the AppHost at a shared or production SQL database unless schema application and outbox dispatch ownership are deliberate.

## Manual Fallback

The AppHost is the preferred full-stack local entry point, but not a replacement for the manual scripts. The documented `run-local.sh`, `seed-local.sh`, `apply-sql-schema-local.sh`, and `drain-outbox-local.sh` paths remain supported for focused troubleshooting and acceptance-test setup.
