## 1. Shared Workflow Boundary

- [x] 1.1 Review the current Billing contracts, topology, local run helpers, and reference acceptance fixtures to choose the smallest shared contract boundary for the two-endpoint workflow.
- [x] 1.2 Add the shared sample contracts needed for Billing to send the Inventory-owned `ReserveInventory` command without coupling Inventory to the Billing Function App executable.
- [x] 1.3 Extend the repo-owned Azure Service Bus emulator topology and sample topology constants with the Inventory queue used by the cross-endpoint command route.

## 2. Inventory Endpoint

- [x] 2.1 Add the sibling Inventory Azure Functions sample host to the solution with its own endpoint name, local configuration, Service Bus queue trigger wrapper, and readable MiniBus registration path.
- [x] 2.2 Add handler-facing Inventory business code that processes `ReserveInventory` through MiniBus APIs while keeping Azure SDK trigger types at the Functions boundary.
- [x] 2.3 Keep Inventory focused on command ownership in this slice without adding SQL reliability wiring, `InventoryReserved` choreography, or a broader order workflow.

## 3. Billing Reference Workflow

- [x] 3.1 Extend the Billing `CreateInvoice` path and explicit transport routes so it sends `ReserveInventory` to the Inventory queue alongside the existing receipt, event, and saga work.
- [x] 3.2 Preserve the existing Billing seed path, emulator workflow, SQL-backed reliability path, and timeout guidance while adapting moved shared contracts where needed.
- [x] 3.3 Update sample guidance so developers can start Billing and Inventory hosts against the emulator, account for host port differences, seed Billing, and observe Inventory processing.

## 4. Reference Verification

- [x] 4.1 Update infrastructure-free reference acceptance coverage so sample-style Billing processing records the outgoing Inventory reservation command with the existing Billing composition outcomes.
- [x] 4.2 Extend emulator-backed acceptance coverage so the Billing workflow dispatches `ReserveInventory` through the repo-owned emulator topology and Inventory processes it through its own endpoint registration path.
- [x] 4.3 Verify the new Inventory host and updated sample solution remain buildable without requiring live Azure Service Bus infrastructure.
- [x] 4.4 Run the focused acceptance and documentation validation that is practical for the two-endpoint workflow and confirm live Azure coverage remains outside this change.
