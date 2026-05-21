# MiniBus.Samples.FunctionApp

This sample is the runnable Billing reference workflow for MiniBus. It keeps Azure Functions wrappers thin while showing the real local path through Azure Service Bus transport:

- manual queue and topic/subscription Service Bus trigger wrappers
- `AddMiniBusAzureFunctions` registration
- `SystemTextJsonMessageSerializer` registration
- a command handler that publishes `InvoiceCreated` and sends `SendInvoiceReceipt`
- a Billing saga that reacts to `InvoiceCreated` and requests a timeout
- real `ServiceBusClient` and `AzureServiceBusSender` registration
- explicit Service Bus routes and recoverability settings

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

## Verification

Build verification remains infrastructure-free. When the compose stack is running, the Service Bus emulator acceptance test drives the sample seed path, processes the received Billing command and event with the same MiniBus processor wiring, verifies the receipt queue and Billing subscription outputs, and exercises timeout scheduling dispatch through the emulator:

```bash
dotnet test tests/MiniBus.AcceptanceTests/MiniBus.AcceptanceTests.csproj --filter FullyQualifiedName~ServiceBusEmulatorBillingWorkflowTests
```

Stop the local Function App before that test so the test owns the `billing-queue` and `billing` subscription consumers. The test ignores older messages from other seeded Billing workflows by matching the unique correlation id it seeds for its own run.

The test skips when the emulator is not reachable on `localhost:5672`. Set `MINIBUS_SERVICEBUS_EMULATOR_CONNECTION_STRING` when the emulator is exposed through another connection string.

## Scope Notes

The emulator is the local reference path, not the cloud parity proof. Data and entities do not persist across emulator container restarts, the emulator omits some Azure Service Bus cloud features, and live Azure Service Bus integration coverage remains follow-up work.

`BillingSaga` uses Azure Service Bus scheduled messages for timeout requests and the emulator acceptance path verifies that scheduling call succeeds. The sample timeout is due after seven days, so the quick local workflow does not wait for the timeout message to be delivered back to the Function App.

Manual wrappers remain the clearest sample default because the trigger boundary stays visible in source. Applications that prefer generated wrappers can reference `MiniBus.AzureFunctions.SourceGenerators` and declare queue or topic/subscription inputs with assembly attributes:

```csharp
using MiniBus.AzureFunctions.SourceGenerators.Declarations;

[assembly: MiniBusSourceGeneratedServiceBusQueueFunction("BillingInput", "billing-queue", "ServiceBus")]
[assembly: MiniBusSourceGeneratedServiceBusTopicFunction("BillingEvents", "domain-events", "billing", "ServiceBus")]
```

SQL inbox/outbox persistence is intentionally not wired into the sample default. Add `MiniBus.Persistence.Sql`, apply the scripts in `src/MiniBus.Persistence.Sql/Schema/`, and schedule outbox draining when the reference workflow needs production SQL reliability guarantees.
