# MiniBus.Samples.FunctionApp

This sample is the runnable Billing reference workflow for MiniBus. It keeps Azure Functions wrappers thin while showing the real local path through Azure Service Bus transport:

- manual queue and topic/subscription Service Bus trigger wrappers
- `AddMiniBusAzureFunctions` registration
- `SystemTextJsonMessageSerializer` registration
- a command handler that publishes `InvoiceCreated` and sends `SendInvoiceReceipt`
- a Billing saga that reacts to `InvoiceCreated` and requests a timeout
- real `ServiceBusClient` and `AzureServiceBusSender` registration
- explicit Service Bus routes and recoverability settings
- an opt-in SQL-backed reliability path for inbox, outbox, and saga state

## Build

The sample still builds without running local infrastructure:

```bash
dotnet build samples/MiniBus.Samples.FunctionApp/MiniBus.Samples.FunctionApp.csproj
```

## Local Billing Workflow

The default workflow uses the Azure Service Bus emulator configuration under `servicebus-emulator/`. Its compose file starts the emulator, its SQL Server dependency, and Azurite for the `AzureWebJobsStorage` value used by the local Functions host.

Prerequisites:

- Docker Desktop with Linux containers
- Azure Functions Core Tools for `func start`

The sample runner starts the local infrastructure, waits for the Service Bus emulator to load the Billing topology, checks the emulator and Azurite ports, then starts the Function App in the foreground:

```bash
ACCEPT_EULA=Y ./samples/MiniBus.Samples.FunctionApp/run-local.sh
```

The script requires `ACCEPT_EULA=Y` so the Azure Service Bus emulator and SQL Server license acceptance is explicit. To start the pieces manually instead, start the compose stack first:

```bash
cd samples/MiniBus.Samples.FunctionApp/servicebus-emulator
ACCEPT_EULA=Y docker compose up -d
```

Wait until the emulator has loaded `Config.json` and created the Billing entities:

```bash
docker compose logs -f emulator
```

Continue after the emulator logs `User defined entities created for SB Emulator` and `Emulator Service is Successfully Up!`. If the Functions host was already polling `billing-queue` before those messages appeared, stop it and run `func start` again after the emulator is ready.

Run the Function App from the sample directory:

```bash
cd samples/MiniBus.Samples.FunctionApp
func start
```

In a second terminal, seed the first Billing command through the repo-owned sender path:

```bash
./samples/MiniBus.Samples.FunctionApp/seed-local.sh
```

If that terminal is already in `samples/MiniBus.Samples.FunctionApp`, use `./seed-local.sh`. The seed script builds the sample and invokes its compiled DLL directly because Azure Functions build targets redirect `dotnet run` for this project into `func host start`.

The seed command sends `CreateInvoice` to the emulator-backed `billing-queue` with MiniBus message-type, message-id, content-type, and correlation metadata. The running Functions host should then show:

1. `BillingInputFunction` processing the command.
2. `CreateInvoiceHandler` logging invoice creation.
3. `Billing sample sent Service Bus message ... to billing-receipts.` for the receipt command.
4. `Billing sample sent Service Bus message ... to domain-events.` for the `InvoiceCreated` publication.
5. `BillingEventsFunction` processing the `billing` subscription copy.
6. `Billing sample scheduled Service Bus message ... to billing-timeouts ...` after the Billing saga requests its timeout.

The default local connection string lives in `local.settings.json`:

```json
"ServiceBus": "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;"
```

Set the `ServiceBus` environment variable before the seed command when it should send to another Service Bus connection string.

## Emulator Topology

The emulator config and `BillingTopology` agree on these entities:

- input queue: `billing-queue`
- outgoing receipt queue: `billing-receipts`
- published event topic: `domain-events`
- Billing event subscription: `billing`
- scheduled timeout queue: `billing-timeouts`
- Functions connection setting: `ServiceBus`

Restart the emulator containers after changing `servicebus-emulator/Config.json`; emulator entity configuration is read when the emulator starts.

## SQL-Backed Billing Workflow

The default emulator workflow stays small and dispatches outgoing work directly. The SQL-backed workflow opts into the production reliability shape already provided by `MiniBus.Persistence.Sql`:

- incoming Billing messages are recorded through the SQL inbox
- outgoing receipt commands, `InvoiceCreated` publications, and timeout schedules are captured in the SQL outbox
- `BillingSaga` state uses SQL saga persistence instead of the sample in-memory saga store
- outbox draining remains an application-owned step

The sample compose file exposes its SQL Server dependency on `localhost:14333` for this reference path. The local `BillingSql` value in `local.settings.json` targets the `master` database in that disposable emulator SQL Server container; set `BillingSql` to a different SQL Server/Azure SQL connection string when the sample should apply scripts to an application-owned database instead.

1. Start the local infrastructure without starting the Function App yet:

   ```bash
   cd samples/MiniBus.Samples.FunctionApp/servicebus-emulator
   ACCEPT_EULA=Y docker compose up -d --force-recreate
   ```

   The SQL-backed workflow exposes the emulator SQL dependency on `localhost:14333`. Recreate the sample stack when this compose shape is first applied or after its `mssql` container is recreated; the Service Bus emulator initializes its own gateway and message-container databases in that SQL service during startup, and Azurite is needed by the local Functions host.

2. From the repository root, apply the packaged MiniBus SQL scripts to the configured Billing SQL database:

   ```bash
   ./samples/MiniBus.Samples.FunctionApp/apply-sql-schema-local.sh
   ```

   The command applies every MiniBus SQL schema script copied into the sample output under `Schema/` in filename order. MiniBus does not apply those scripts automatically when SQL persistence is enabled.

3. Start the Function App with SQL persistence enabled:

   ```bash
   cd samples/MiniBus.Samples.FunctionApp
   BillingSqlEnabled=true func start
   ```

4. From the repository root, seed the same Billing command from another terminal:

   ```bash
   ./samples/MiniBus.Samples.FunctionApp/seed-local.sh
   ```

5. From the repository root, drain the outbox after `BillingInputFunction` has processed the command:

   ```bash
   ./samples/MiniBus.Samples.FunctionApp/drain-outbox-local.sh
   ```

   The first drain sends the captured receipt command and `InvoiceCreated` event through the configured Service Bus routes. After `BillingEventsFunction` processes the event, run the drain command again to dispatch the captured `InvoicePaymentTimeout` schedule. The command reports how many pending Billing outbox operations were dispatched each time.

Set `BillingSql` before `apply-sql-schema-local.sh`, `func start`, and `drain-outbox-local.sh` when the SQL-backed workflow should use another connection string. Set `BillingSqlSchema` only after adapting the schema scripts for that schema through the application deployment flow; the sample schema command intentionally applies the packaged default `MiniBus` schema scripts unchanged.

## Verification

Build verification remains infrastructure-free. When the compose stack is running, the Service Bus emulator acceptance test drives the sample seed path, processes the received Billing command and event with the same MiniBus processor wiring, verifies the receipt queue and Billing subscription outputs, and exercises timeout scheduling dispatch through the emulator:

```bash
dotnet test tests/MiniBus.AcceptanceTests/MiniBus.AcceptanceTests.csproj --filter FullyQualifiedName~ServiceBusEmulatorBillingWorkflowTests
```

Stop the local Function App before that test so the test owns the `billing-queue` and `billing` subscription consumers. The SQL-backed emulator scenario uses the same local SQL endpoint exposed by the compose stack, applies the packaged schema scripts, captures outgoing work in SQL, and drains it through the configured Service Bus transport. The tests ignore older messages from other seeded Billing workflows by matching the unique correlation id they seed for their own run.

The test skips when the emulator is not reachable on `localhost:5672`. Set `MINIBUS_SERVICEBUS_EMULATOR_CONNECTION_STRING` when the emulator is exposed through another connection string.

## Scope Notes

The emulator is the local reference path, not the cloud parity proof. Data and entities do not persist across emulator container restarts, the emulator omits some Azure Service Bus cloud features, and live Azure Service Bus integration coverage remains follow-up work.

`BillingSaga` uses Azure Service Bus scheduled messages for timeout requests and the emulator acceptance path verifies that scheduling call succeeds, directly or after SQL outbox drain depending on the workflow. The sample timeout is due after seven days, so the quick local workflow does not wait for the timeout message to be delivered back to the Function App.

Manual wrappers remain the clearest sample default because the trigger boundary stays visible in source. Applications that prefer generated wrappers can reference `MiniBus.AzureFunctions.SourceGenerators` and declare queue or topic/subscription inputs with assembly attributes:

```csharp
using MiniBus.AzureFunctions.SourceGenerators.Declarations;

[assembly: MiniBusSourceGeneratedServiceBusQueueFunction("BillingInput", "billing-queue", "ServiceBus")]
[assembly: MiniBusSourceGeneratedServiceBusTopicFunction("BillingEvents", "domain-events", "billing", "ServiceBus")]
```

SQL inbox/outbox and saga persistence remain opt-in in the sample. The SQL-backed workflow above keeps schema application and outbox draining visible because production applications own those deployment and scheduling choices.
