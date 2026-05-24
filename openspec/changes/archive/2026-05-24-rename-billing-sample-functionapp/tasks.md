## 1. Rename Billing Sample Project

- [x] 1.1 Move `samples/MiniBus.Samples.FunctionApp` to `samples/MiniBus.Samples.Billing.FunctionApp`.
- [x] 1.2 Rename `MiniBus.Samples.FunctionApp.csproj` to `MiniBus.Samples.Billing.FunctionApp.csproj` and ensure the project emits the `MiniBus.Samples.Billing.FunctionApp` assembly and root namespace.
- [x] 1.3 Update Billing sample source namespaces and internal `using` directives from `MiniBus.Samples.FunctionApp` to `MiniBus.Samples.Billing.FunctionApp`.

## 2. Update Repository References

- [x] 2.1 Update `MiniBus.sln` and acceptance-test project references to point at the renamed Billing sample project.
- [x] 2.2 Update Billing local workflow scripts so project and DLL paths use `MiniBus.Samples.Billing.FunctionApp`.
- [x] 2.3 Update active documentation references in the root README, package READMEs, Billing and Inventory sample READMEs, and other non-archived docs.
- [x] 2.4 Update `openspec/project.md` to describe the renamed sample layout and remove the completed backlog item.

## 3. Preserve Workflows

- [x] 3.1 Verify the Billing emulator workflow instructions still point to the repo-owned Service Bus emulator topology and keep existing queue, topic, subscription, and endpoint names.
- [x] 3.2 Verify the SQL-backed Billing workflow still documents schema setup, SQL persistence registration, seeding, and manual outbox draining after the rename.
- [x] 3.3 Verify Inventory sample documentation and cross-endpoint references point to the renamed Billing sample where applicable.

## 4. Verification

- [x] 4.1 Run a repository search for `MiniBus.Samples.FunctionApp` and `samples/MiniBus.Samples.FunctionApp` and update all active references; leave archived OpenSpec history unchanged unless it affects current guidance.
- [x] 4.2 Build `MiniBus.sln` or the affected sample and acceptance projects to catch stale project, namespace, and assembly references.
- [x] 4.3 Run focused acceptance tests that compile against the renamed Billing sample registration, or document why they were not run.
