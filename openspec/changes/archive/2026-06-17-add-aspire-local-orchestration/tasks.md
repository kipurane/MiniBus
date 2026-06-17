## 1. AppHost Setup

- [x] 1.1 Add a dedicated Aspire AppHost project under `samples/` and include it in `MiniBus.sln`.
- [x] 1.2 Add AppHost-only Aspire hosting package references without adding Aspire dependencies to MiniBus runtime, persistence, transport, tooling, or Function App packages.
- [x] 1.3 Define stable AppHost resource names for SQL, Service Bus emulator support, Billing, Inventory, Billing outbox dispatcher, and Tooling Web.

## 2. Local Infrastructure Wiring

- [x] 2.1 Configure the AppHost SQL resource or connection string source for the Billing SQL persistence database.
- [x] 2.2 Configure Azure Service Bus emulator support using the existing sample topology names and connection-string shape.
- [x] 2.3 Configure local Functions storage for the orchestrated Function Apps.
- [x] 2.4 Preserve the existing compose and script-based emulator path as a supported fallback.

## 3. Project Orchestration

- [x] 3.1 Add Billing Function App orchestration with SQL persistence enabled and shared `BillingSql`, `BillingSqlSchema`, `ServiceBus`, and Functions storage settings.
- [x] 3.2 Add Inventory Function App orchestration with the shared `ServiceBus` and Functions storage settings.
- [x] 3.3 Add Billing outbox dispatcher Function App orchestration with shared SQL, Service Bus, schedule, and drain-bound settings.
- [x] 3.4 Add `MiniBus.Tooling.Web` orchestration configured with `MiniBus:Tooling:Sql:ConnectionString` and `MiniBus:Tooling:Sql:SchemaName` targeting the Billing SQL database.

## 4. Schema And Workflow Experience

- [x] 4.1 Decide whether SQL schema application is documented as a separate command or exposed as an explicit local helper step.
- [x] 4.2 Ensure the Aspire workflow keeps schema application visible and does not silently mutate arbitrary SQL targets.
- [x] 4.3 Document how to seed the Billing workflow and observe Billing, Inventory, outbox dispatch, and Tooling Web state under Aspire.

## 5. Documentation And Backlog

- [x] 5.1 Update sample documentation with Aspire prerequisites, license acceptance, run commands, ports/endpoints, schema setup, and troubleshooting notes.
- [x] 5.2 Update root documentation to point to the Aspire local orchestration path while preserving manual setup guidance.
- [x] 5.3 Update `openspec/project.md` to mark Aspire-based local orchestration complete once implementation and verification land.

## 6. Verification

- [x] 6.1 Add focused tests or checks that verify the AppHost project references and required configuration keys remain coherent.
- [x] 6.2 Build the AppHost project.
- [x] 6.3 Run relevant existing sample/tooling build or test commands affected by the orchestration wiring.
- [x] 6.4 Confirm distributable MiniBus packages do not gain unintended Aspire package dependencies.
