## Context

MiniBus already has two complementary reference paths. `samples/MiniBus.Samples.FunctionApp` is the emulator-runnable Billing Function App that keeps Azure Functions wrappers thin and proves the local Azure Service Bus flow for command handling, event publication, receipt-command dispatch, and saga timeout scheduling. The acceptance suite also has SQL-backed Billing scenarios that prove the existing inbox, outbox, SQL saga persistence, and outbox dispatcher can compose through sample-style registration without live Azure Service Bus.

Those paths are still separate in the developer story. The sample default uses in-memory saga persistence and explicitly leaves SQL reliability wiring out of the local Billing workflow, while the root guidance asks applications to apply schema scripts and own outbox draining themselves. This change should connect the stories without turning the sample into a hidden provisioning system or a second framework surface.

## Goals / Non-Goals

**Goals:**

- Add an optional SQL-backed Billing reference path alongside the existing lightweight emulator workflow.
- Show the existing SQL schema script application, SQL persistence registration, SQL-backed saga state, and outbox drain responsibilities in the Billing sample story.
- Keep the Billing architecture recognizable: thin Azure Functions wrappers, transport-independent handlers, explicit Service Bus routes, and the existing saga timeout route.
- Strengthen acceptance coverage for the SQL-backed Billing composition path, including inbox duplicate behavior and outbox draining.
- Keep local verification layered so infrastructure-free tests remain cheap while SQL-backed and emulator-backed checks prove the richer paths when their local dependencies are available.

**Non-Goals:**

- Add new SQL persistence abstractions, schema semantics, or runtime reliability primitives beyond a small integration gap discovered while composing the sample.
- Replace the existing simple emulator workflow with SQL as the only sample mode.
- Add Inventory or other multi-endpoint sample workflows.
- Add live Azure Service Bus proof coverage, Azure provisioning automation, NuGet publishing automation, new topology models, dashboards, or manual retry tooling.

## Decisions

### Keep one Billing sample with an opt-in SQL reliability path

The SQL-backed workflow should extend `samples/MiniBus.Samples.FunctionApp` rather than creating a shadow SQL sample. The existing emulator workflow remains the first-run loop; the SQL-backed path adds explicit configuration, local setup, and drain steps for developers who need the production reliability shape.

Alternative considered: make the default Billing sample SQL-backed. That would make the first local run heavier and blur the distinction between the small framework path and the reliability path applications opt into.

Alternative considered: create a second SQL Billing sample. That would duplicate the same wrappers, contracts, routes, handlers, and saga code, making the two reference stories drift.

### Keep SQL ownership explicit in the sample

The SQL-backed path should show schema application and SQL persistence registration directly and should invoke the existing `SqlMiniBusOutboxDispatcher` through an application-owned drain path. The sample may provide repository-owned helpers or local scripts to make those steps repeatable, but it should not imply that MiniBus provisions SQL or drains outbox work automatically.

Alternative considered: hide schema and drain setup behind new framework automation. That contradicts the current SQL package contract and would turn a reference workflow into an accidental product feature.

### Reuse current local infrastructure patterns without coupling the runtime to them

The SQL-backed sample path should use explicit connection configuration and repository-owned local guidance or assets so it can run against a local SQL Server path where practical. Verification should continue to use the existing SQL Server Testcontainers or documented external SQL Server/Azure SQL connection-string behavior for SQL-gated acceptance scenarios, and should keep live Azure Service Bus out of the required proof layer.

Alternative considered: make a live Azure namespace the SQL-backed reference target. That would move the sample beyond the backlog order and make the SQL story harder to iterate locally.

### Verify cross-package composition rather than SQL internals

Acceptance coverage should prove the Billing workflow records inbox/outbox/saga effects, skips a duplicate Billing delivery through the inbox path, and drains persisted send, publish, and schedule work through the configured transport abstraction. Low-level claim leases, row shapes, and SQL failure mechanics remain covered by SQL persistence tests.

Alternative considered: assert every SQL outbox and inbox column from the reference tests. That would duplicate lower-level coverage and make the sample proof brittle.

## Risks / Trade-offs

- [Risk] A SQL-backed sample mode can make the Billing sample look more complex than the starter path. -> Keep the default emulator workflow intact and document the SQL path as the reliability increment.
- [Risk] Local Service Bus emulator setup and local SQL setup can create a heavy developer loop. -> Keep build and Tier 1 verification infrastructure-free and make SQL/emulator checks opt into the local dependencies they actually need.
- [Risk] Sample helpers could accidentally imply automatic SQL provisioning or background outbox dispatch. -> Name and document schema application and drain steps as application-owned responsibilities.
- [Risk] Acceptance scenarios could overfit sample implementation details. -> Assert durable processing and transport-visible outcomes at the composition level and leave SQL internals to existing SQL tests.

## Migration Plan

1. Preserve the existing Billing sample path and its public handler-facing structure.
2. Add the opt-in SQL configuration and local setup path next to the current emulator workflow.
3. Show explicit schema application and outbox draining in sample guidance and verification.
4. Keep later Inventory, live Azure, provisioning, and distribution changes separate after the SQL-backed Billing story is stable.

## Open Questions

- Should the local SQL-backed run command extend the existing Billing run helper with an explicit SQL mode, or should the SQL path have a separate helper so the lightweight emulator loop stays visually separate?
