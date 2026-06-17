## Context

MiniBus already has a realistic local reference stack: Billing and Inventory Azure Functions samples, an Azure Service Bus emulator compose setup, SQL persistence scripts, a separate Billing SQL outbox dispatcher Function App, and `MiniBus.Tooling.Web` for SQL-backed operational inspection. The current workflow is intentionally transparent, but it requires several terminals and careful configuration alignment across Service Bus, SQL, Functions storage, the dispatcher, and tooling.

The project backlog already identifies Aspire as the preferred local composition tool for SQL Server, the Service Bus emulator, Function Apps, dispatcher hosts, and `MiniBus.Tooling.Web`. Aspire should make the reference system easier to run without changing MiniBus runtime package dependencies or hiding production-owned choices such as schema application and outbox dispatch scheduling.

## Goals / Non-Goals

**Goals:**

- Add a sample/development Aspire AppHost that starts the local MiniBus reference environment as one coordinated system.
- Orchestrate SQL Server, Azure Service Bus emulator support, Billing Function App, Inventory Function App, Billing outbox dispatcher Function App, and `MiniBus.Tooling.Web`.
- Provide one configuration source for the shared Service Bus, Billing SQL, schema, Functions storage, and tooling SQL settings used by the orchestrated services.
- Keep existing manual scripts and compose instructions valid for developers who want to run pieces independently.
- Document the orchestrated workflow, prerequisite license acceptance, schema setup behavior, and troubleshooting boundaries.
- Add build/configuration verification that can run without requiring live Azure infrastructure.

**Non-Goals:**

- Provision live Azure resources or add cloud deployment templates.
- Replace the existing sample scripts or emulator compose path.
- Add DLQ resubmission, message replay, browser-triggered mutations, or Azure Service Bus tooling inspection.
- Move Aspire dependencies into MiniBus runtime, tooling, transport, persistence, or sample Function App packages unless the dependency is isolated to the AppHost.
- Change public MiniBus runtime APIs except for small configuration adjustments that are necessary to make existing samples externally orchestratable.

## Decisions

1. Add a dedicated sample AppHost project.

   The AppHost should live under `samples/` as the owner of local composition, for example `samples/MiniBus.Samples.AppHost`. It should be referenced by the solution but remain outside distributable `src/*` packages.

   Alternative considered: add Aspire hosting code to `MiniBus.Tooling.Web` or one of the Function Apps. That would blur runtime/tooling responsibilities and make orchestration look like a package feature instead of a development sample concern.

2. Prefer reusing the existing Service Bus emulator assets for the first slice.

   The existing emulator `Config.json` and compose setup are already the source of truth for local topology. The AppHost should either wrap those assets or reproduce their container/resource shape with the same config file and connection settings. If direct Aspire container modeling is too fragile for the emulator image, the first implementation can document that the AppHost coordinates app projects while the existing compose stack owns emulator infrastructure.

   Alternative considered: replace the compose setup entirely. That would create churn around a working local path and risk losing emulator-specific setup details.

3. Keep SQL schema application explicit unless the implementation can automate it safely.

   MiniBus does not auto-apply persistence scripts at runtime. The AppHost documentation should make schema setup visible and may expose a helper/resource step for the existing `apply-sql-schema-local.sh` flow, but the AppHost should not silently mutate arbitrary SQL targets.

   Alternative considered: always apply schema on AppHost startup. That is convenient for disposable local containers, but it weakens the existing production guidance that applications own schema deployment.

4. Run the existing dispatcher Function App as the orchestrated outbox drain host.

   The separate timer-triggered Billing outbox dispatcher already demonstrates the production-style ownership boundary: Billing processing owns message handling and SQL commits, while the dispatcher owns scheduled outbox draining. The AppHost should compose that existing host rather than introduce a second worker model in the first slice.

   Alternative considered: add a simpler worker just for Aspire. That could reduce Functions startup complexity, but it would create another sample shape before the current dispatcher reference path has been fully exercised.

5. Configure `MiniBus.Tooling.Web` against the same Billing SQL database.

   The AppHost should pass the same Billing SQL connection string and schema settings to `MiniBus.Tooling.Web` through `MiniBus:Tooling:Sql:*` configuration so the UI shows the state produced by the orchestrated Billing workflow.

   Alternative considered: leave tooling configuration manual. That would undercut the main value of orchestration: seeing message processing, durable state, and local tooling together.

## Risks / Trade-offs

- [Risk] Azure Functions projects may require Core Tools behavior that is awkward under Aspire project orchestration. -> Mitigation: validate the chosen hosting path early and keep a fallback where AppHost coordinates infrastructure plus documented `func start` commands if necessary.
- [Risk] The Azure Service Bus emulator has license acceptance and container-specific requirements. -> Mitigation: require explicit local license acceptance and preserve the existing compose path as the authoritative fallback.
- [Risk] Automatic SQL schema setup could surprise developers. -> Mitigation: keep schema application explicit or limit automation to disposable local containers with clear documentation.
- [Risk] The AppHost can drift from the manual sample scripts. -> Mitigation: share the same topology names, connection setting names, and SQL schema defaults; add focused tests or build checks for project references and documented configuration keys.
- [Risk] Adding Aspire dependencies can accidentally affect package output. -> Mitigation: keep AppHost dependencies isolated under `samples/` and verify distributable package metadata remains unchanged.

## Migration Plan

No runtime migration is required. Existing sample scripts, local settings, compose commands, and tests continue to work. The new AppHost becomes an additional local entry point and documentation can gradually point contributors to it as the preferred full-stack local workflow.

Rollback is straightforward: remove the AppHost project, solution reference, and docs while leaving the existing sample infrastructure unchanged.

## Open Questions

- Can the Azure Service Bus emulator be modeled directly as Aspire container resources with the existing `Config.json`, or should the first implementation invoke/reuse the existing compose stack?
- Should schema application be represented as an explicit AppHost helper step, documented as a separate command, or automated only for disposable local SQL containers?
- What is the most reliable way to start Azure Functions sample projects from Aspire in this repository's target .NET and Functions worker versions?
