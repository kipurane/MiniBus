## Why

The Billing sample still uses the generic `MiniBus.Samples.FunctionApp` name while the newer Inventory endpoint already uses `MiniBus.Samples.Inventory.FunctionApp`. Renaming Billing to `MiniBus.Samples.Billing.FunctionApp` makes the two-endpoint reference workflow easier to scan and keeps sample names aligned with endpoint ownership.

## What Changes

- Rename the Billing sample directory and project from `samples/MiniBus.Samples.FunctionApp` / `MiniBus.Samples.FunctionApp.csproj` to `samples/MiniBus.Samples.Billing.FunctionApp` / `MiniBus.Samples.Billing.FunctionApp.csproj`.
- Update Billing sample namespaces, solution entries, project references, shell scripts, documentation, and acceptance-test references that depend on the old path, project name, assembly name, or namespace.
- Preserve the existing Billing emulator workflow, SQL-backed reliability workflow, manual drain scripts, Inventory endpoint interaction, and acceptance coverage.
- Update OpenSpec project context and active backlog references so the rename is no longer listed as pending after implementation.
- Do not change MiniBus runtime APIs, transport behavior, persistence behavior, or the sample's business workflow.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `buildable-functionapp-sample`: require the Billing sample to use the endpoint-specific `MiniBus.Samples.Billing.FunctionApp` identity and path while preserving the existing runnable Billing and Inventory reference workflow.

## Impact

- Affected sample project: `samples/MiniBus.Samples.FunctionApp`, renamed to `samples/MiniBus.Samples.Billing.FunctionApp`.
- Affected solution and project references: `MiniBus.sln`, acceptance-test project references, and any scripts that invoke the Billing sample project or compiled DLL.
- Affected source names: Billing sample namespaces and any `using MiniBus.Samples.FunctionApp` references in tests or sibling samples.
- Affected documentation: root README, package READMEs that point to the Billing sample, sample README files, and `openspec/project.md`.
- No expected dependency, API, schema, or package behavior changes.
