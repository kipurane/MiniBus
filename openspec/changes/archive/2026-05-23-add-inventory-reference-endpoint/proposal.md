## Why

The emulator-backed Billing workflow now proves the single-endpoint Azure Functions reference path, but MiniBus does not yet show a local command crossing a real endpoint boundary. Adding a small Inventory endpoint is the next developer-experience step because it can build on the existing emulator assets without turning the sample into a broad business application.

## What Changes

- Extend the runnable reference workflow from Billing alone to Billing plus a sibling Inventory Azure Functions endpoint.
- Keep Billing as the seeded entry point and have its `CreateInvoice` flow send a cross-endpoint `ReserveInventory` command to the Inventory-owned queue.
- Add the shared sample contract boundary, Inventory host code, emulator topology, run guidance, and local configuration needed for Inventory to process that command independently from Billing.
- Extend high-level verification so ordinary builds remain infrastructure-free while emulator-backed acceptance proves the Billing-to-Inventory command path.
- Keep the existing Billing SQL-backed reliability path intact without requiring Inventory SQL persistence, broader order orchestration, `InventoryReserved` round trips, or live Azure Service Bus coverage in this first multi-endpoint slice.

## Capabilities

### New Capabilities

- None.

### Modified Capabilities

- `buildable-functionapp-sample`: Extend the emulator-backed reference sample contract from a runnable Billing endpoint to a focused two-endpoint Billing and Inventory workflow with a shared cross-endpoint command.
- `reference-solution-acceptance-tests`: Extend high-level reference verification expectations so the two-endpoint sample path is covered at the appropriate infrastructure-free and emulator-backed levels.

## Impact

- Sample projects, contracts, local host guidance, and solution inclusion under `samples/`.
- Billing sample registration, routing, handler behavior, emulator topology, and workflow documentation.
- A new Inventory Azure Functions sample endpoint with its own queue trigger wrapper, endpoint registration, and handler-facing business code.
- Acceptance coverage that exercises the new cross-endpoint command route without expanding MiniBus runtime APIs.
