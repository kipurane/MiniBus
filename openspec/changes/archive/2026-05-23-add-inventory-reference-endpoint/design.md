## Context

`samples/MiniBus.Samples.FunctionApp` is now the emulator-runnable Billing reference workflow. It already proves the local Azure Service Bus path for a single MiniBus endpoint: one Azure Functions host processes seeded Billing commands, dispatches receipt commands and published events through explicit Service Bus routes, observes the Billing subscription through a thin trigger wrapper, and keeps the SQL-backed reliability path opt-in.

The next sample increment needs to show what changes when a command belongs to another endpoint. MiniBus documentation defines an endpoint as a logical message processor that usually maps to one Function App, one input queue, one handler set, one endpoint name, and one persistence configuration. The current Azure Functions registration shape follows that model with a single processor and endpoint options per host service provider. The reference sample should reinforce that boundary rather than making Inventory look like a Billing handler folder.

## Goals / Non-Goals

**Goals:**

- Extend the existing emulator-backed Billing workflow with a second real endpoint that owns Inventory command processing.
- Use the cross-endpoint command route as the new teaching point while preserving the already-proven Billing send, publish, saga, SQL, and seed behavior.
- Keep the sample projects and shared contracts legible enough that developers can see which messages are shared across endpoint boundaries.
- Add the emulator topology, local run guidance, and high-level verification needed to observe Billing and Inventory together without requiring live Azure resources.

**Non-Goals:**

- Collapse Billing and Inventory into one Azure Functions host or one MiniBus endpoint identity.
- Build a broader Orders, Billing, and Inventory business workflow or add endpoint orchestration that the first boundary example does not need.
- Add an `InventoryReserved` event round trip without a concrete sample consumer.
- Duplicate the Billing SQL reliability path for Inventory or change MiniBus runtime APIs to support the sample.
- Add live Azure Service Bus provisioning or proof coverage.

## Decisions

### Add Inventory as a sibling Azure Functions endpoint

Create a sibling Inventory sample host with its own Function App entry point, `Inventory` endpoint registration, input queue trigger wrapper, and `ReserveInventory` handler. Billing remains the entry point for the reference workflow and sends the Inventory-owned command through the normal MiniBus transport route.

Alternative considered: register Inventory handlers inside the Billing Function App. That would be smaller, but it would teach one processor handling more work rather than a command crossing the endpoint boundary MiniBus models.

Alternative considered: turn the first multi-endpoint example into a larger reference solution with more business services. That would obscure the specific boundary lesson and multiply local orchestration before the simpler two-endpoint path is useful.

### Use `ReserveInventory` as the cross-endpoint contract

Extend the existing `CreateInvoice` path so Billing sends `ReserveInventory` to `inventory-queue`. Inventory owns that queue and handles the command. The first slice stops there: Billing still publishes `InvoiceCreated`, sends its receipt command, and runs its existing saga path, while Inventory shows command ownership independently.

Alternative considered: add Inventory only as a subscriber to `InvoiceCreated`. The existing Billing workflow already proves topic and subscription processing, so an event-only subscriber would add less to the reference story than an owned command queue.

Alternative considered: have Inventory publish `InventoryReserved` back immediately. That would be a valid later choreography example, but without a consumer it adds contracts, routing, topology, and assertions that do not make the first endpoint boundary clearer.

### Move shared sample messages behind a clear contract boundary

Messages used across the Billing and Inventory projects should live in a small shared sample contracts project or an equivalently explicit shared boundary under `samples/`. Endpoint hosts should reference that contract surface rather than making Inventory depend on a Billing executable project for `ReserveInventory`.

Alternative considered: keep all contracts inside `MiniBus.Samples.FunctionApp`. That matches the current one-host sample but makes the new endpoint look coupled to Billing implementation details.

Alternative considered: create a broad sample platform project that owns contracts, topology, scripts, and helpers. That is more structure than the first shared-message boundary needs.

### Reuse the Billing emulator workflow as the reference anchor

Keep the repo-owned emulator stack and Billing seed path as the local workflow anchor. Extend the topology with `inventory-queue`, document how to start the second Functions host alongside Billing including any distinct Core Tools port setting, and surface the Inventory processing log in the observable workflow steps. Prefer extending the existing sample guidance or adding a small higher-level guide only when the two-host instructions would make the Billing README misleading.

Alternative considered: split a new multi-endpoint sample runner away from the current Billing workflow immediately. That would duplicate the emulator bootstrap and seed story before the second endpoint proves that such a split is needed.

### Keep verification layered

Normal build and infrastructure-free acceptance should continue to prove sample-style composition cheaply, including Billing capture of the Inventory command route. Emulator-gated acceptance should prove the transport-visible boundary by dispatching the Billing workflow through the emulator and letting the Inventory endpoint processing path consume the Inventory command. The Billing SQL scenarios remain focused on the existing SQL-backed Billing path.

Alternative considered: verify the new endpoint only through manual docs. The new host, shared contracts, emulator topology, route, and acceptance helpers would drift easily without automated boundary coverage.

## Risks / Trade-offs

- [Risk] Two sample hosts can make the local workflow harder to start and observe. -> Keep Billing as the seeded entry point, document the second host and its port clearly, and avoid adding more endpoints in the same slice.
- [Risk] Moving shared contracts can create churn in the already-stable Billing sample. -> Move only contracts that must cross sample project boundaries and keep Billing-specific implementation code where it is.
- [Risk] Inventory can look artificially small next to Billing. -> Treat that asymmetry as intentional: Inventory demonstrates endpoint ownership while Billing remains the richer reference anchor.
- [Risk] Two-endpoint verification can become a second full test matrix. -> Add boundary-focused build and acceptance checks, and leave Billing SQL, transport internals, and live Azure concerns in their existing proof layers.

## Migration Plan

1. Preserve the existing Billing local workflow as the seed and reference anchor.
2. Introduce the shared command contract and Inventory sibling endpoint, then wire Billing routing to the Inventory queue.
3. Extend the emulator topology, run guidance, and acceptance path to cover the two hosts together.
4. Keep later Inventory events, broader workflows, Inventory SQL reliability, and live Azure coverage as follow-up decisions.

## Open Questions

- Should the two-host run guidance stay in the existing Billing README with a renamed reference-workflow section, or should the shared workflow gain a small top-level sample guide while Billing keeps endpoint-specific details?
