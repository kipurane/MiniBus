## Why

MiniBus now has an emulator-runnable Billing reference workflow and the SQL reliability pieces needed for production-oriented message processing, but those two stories still meet only in lower-level tests and setup guidance. The next production-readiness step should show how the existing SQL inbox, outbox, and saga persistence features compose inside the Billing Azure Functions path before the project expands to more samples or live Azure proof coverage.

## What Changes

- Extend the existing Billing reference workflow with an explicit SQL-backed path that keeps the current emulator topology, thin Azure Functions wrappers, handlers, event flow, and saga timeout scheduling visible.
- Demonstrate SQL persistence registration and schema setup for Billing, including SQL inbox duplicate detection, SQL outbox capture for outgoing send/publish/schedule work, and SQL saga persistence for the Billing saga.
- Add a repository-owned way to drain captured Billing outbox work through the existing SQL outbox dispatcher so the reference path shows the application-owned dispatch responsibility.
- Add or extend cross-package verification for the SQL-backed Billing workflow using the existing local SQL and Service Bus emulator patterns where practical.
- Update sample and root guidance so developers can distinguish the lightweight emulator workflow from the SQL-backed reliable workflow and understand the local setup limits.
- Keep broader runtime primitives, Inventory or multi-endpoint expansion, live Azure Service Bus coverage, automatic infrastructure provisioning, publishing automation, new topology models, dashboards, and manual retry tooling out of scope.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `buildable-functionapp-sample`: Extend the Billing sample contract from an emulator-backed local workflow to an optional SQL-backed reliability path with explicit schema setup, persistence registration, and outbox draining guidance.
- `reference-solution-acceptance-tests`: Strengthen cross-package acceptance coverage for the SQL-backed Billing reference workflow so the reference path proves inbox, outbox, saga persistence, and outbox dispatch composition rather than only isolated SQL behavior.

## Impact

- Billing sample code, local infrastructure assets, and documentation under `samples/MiniBus.Samples.FunctionApp`.
- Root guidance for the recommended early MiniBus workflow and the distinction between simple and SQL-backed sample paths.
- Acceptance coverage under `tests/MiniBus.AcceptanceTests` that composes Azure Functions processing, Azure Service Bus reference flow, and SQL persistence behavior.
- OpenSpec deltas for the Function App sample and reference-solution acceptance expectations.
- No expected new public MiniBus runtime abstraction, SQL schema model, Azure topology model, or live Azure dependency.
