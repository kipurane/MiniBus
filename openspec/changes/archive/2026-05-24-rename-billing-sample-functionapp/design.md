## Context

The reference workflow now has two Azure Functions sample endpoints: Billing and Inventory. Inventory already uses an endpoint-specific directory, project, assembly, and namespace (`MiniBus.Samples.Inventory.FunctionApp`), while Billing still uses the older generic identity (`MiniBus.Samples.FunctionApp`). This makes the sample tree asymmetric and leaves references that are easy to misread once more endpoint samples are added.

The change is a repository hygiene and developer-experience rename. It touches several files because project identity appears in the solution, project references, namespaces, shell scripts, README paths, acceptance tests, and OpenSpec project context. It must not alter message contracts, endpoint names, queue/topic topology, SQL persistence behavior, or runtime APIs.

## Goals / Non-Goals

**Goals:**

- Rename the Billing sample project identity to `MiniBus.Samples.Billing.FunctionApp`.
- Move the Billing sample to `samples/MiniBus.Samples.Billing.FunctionApp`.
- Update all non-archived references that need to compile, run, or guide developers after the rename.
- Preserve local emulator, SQL-backed reliability, manual outbox drain, and acceptance-test workflows.
- Leave archived OpenSpec history intact unless a live reference outside the active project context requires an update.

**Non-Goals:**

- Changing Billing endpoint names, Azure Service Bus entity names, or sample message contracts.
- Refactoring sample structure beyond the rename.
- Introducing new sample capabilities, runtime APIs, or persistence/transport behavior.
- Renaming the Inventory sample or shared contracts project.

## Decisions

1. Use a full project identity rename, not only a directory rename.

   The project file, assembly name, root namespace, source namespaces, test `using` directives, and script DLL paths should all move to `MiniBus.Samples.Billing.FunctionApp`. Keeping the old project identity inside a new folder would reduce path inconsistency but leave the same ambiguity in code, build output, and logs.

   Alternative considered: move only the directory and keep `MiniBus.Samples.FunctionApp` as the project/namespace. Rejected because the primary discoverability problem appears in both filesystem paths and project identities.

2. Preserve sample workflow names and infrastructure names.

   Function names such as `BillingInput`, endpoint names such as `Billing`, and emulator entities such as `billing-queue` should remain unchanged. The rename is about the host project identity, not operational topology.

   Alternative considered: rename more Billing-facing symbols for perfect symmetry. Rejected because that would turn a low-risk sample identity change into a broader behavioral and documentation churn.

3. Update active documentation and generated-facing references, but keep archived proposals historical.

   Current docs, package READMEs, sample READMEs, acceptance tests, scripts, solution entries, and `openspec/project.md` should use the new identity. Archived OpenSpec changes may continue to describe the name that existed when those changes were written.

   Alternative considered: rewrite archived OpenSpec history. Rejected because archives are historical records and broad edits there create noise without improving the current developer path.

## Risks / Trade-offs

- Broken build references after the move -> mitigate with `dotnet build MiniBus.sln` or targeted builds that include sample and acceptance projects.
- Broken local scripts due to compiled DLL path changes -> mitigate by updating `run-local.sh`, `seed-local.sh`, `apply-sql-schema-local.sh`, and `drain-outbox-local.sh`, then checking references with `rg`.
- Missed documentation paths -> mitigate with a repository-wide search for `MiniBus.Samples.FunctionApp` and old `samples/MiniBus.Samples.FunctionApp` paths after implementation.
- Renaming namespaces increases diff size -> accept because it keeps project identity coherent across filesystem, assembly, and source code.
