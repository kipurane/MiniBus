# MiniBus.Samples.Inventory.FunctionApp

This sample is the Inventory endpoint for the emulator-backed Billing reference workflow. It owns `inventory-queue`, processes `ReserveInventory`, and keeps its handler code independent from Azure SDK trigger types.

Build it without local infrastructure:

```bash
dotnet build samples/MiniBus.Samples.Inventory.FunctionApp/MiniBus.Samples.Inventory.FunctionApp.csproj
```

Run it after the Billing sample emulator stack is ready:

```bash
./samples/MiniBus.Samples.Inventory.FunctionApp/run-local.sh
```

The script starts Azure Functions Core Tools on port `7072` by default so it can run beside the Billing Function App. Set `MINIBUS_INVENTORY_FUNCTIONS_PORT` when another port is needed.

Seed the workflow through the Billing sample:

```bash
./samples/MiniBus.Samples.Billing.FunctionApp/seed-local.sh
```

Inventory should log that `ReserveInventoryHandler` reserved the sample SKU for the seeded invoice.
