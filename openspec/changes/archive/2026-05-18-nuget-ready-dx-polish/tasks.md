## 1. Package Metadata

- [x] 1.1 Identify the intended package projects under `src/` and confirm tests and samples are not intended NuGet packages.
- [x] 1.2 Add repository-level MSBuild package/build conventions for shared metadata, deterministic Release packing, symbol/readme conventions, and package output.
- [x] 1.3 Add package-specific metadata to `MiniBus.Core`, including package id, title, description, tags, and package README configuration.
- [x] 1.4 Add package-specific metadata to `MiniBus.AzureServiceBus`, including package id, title, description, tags, and package README configuration.
- [x] 1.5 Add package-specific metadata to `MiniBus.AzureFunctions`, including package id, title, description, tags, and package README configuration.
- [x] 1.6 Add package-specific metadata to `MiniBus.Persistence.Sql`, including package id, title, description, tags, and package README configuration.
- [x] 1.7 Add package-specific metadata to `MiniBus.Persistence.AzureStorage`, including package id, title, description, tags, and package README configuration.
- [x] 1.8 Add package-specific metadata to `MiniBus.Testing`, including package id, title, description, tags, and package README configuration.
- [x] 1.9 Explicitly mark test and sample projects non-packable where needed.

## 2. Documentation Polish

- [x] 2.1 Update the root README with a concise golden-path setup flow for core contracts, handlers, Azure Functions manual wrappers, Azure Service Bus routing, SQL persistence, recoverability, observability, and testing.
- [x] 2.2 Update root README status and limitations so source generators, analyzers, templates, live Azure integration tests, publishing, and automatic infrastructure provisioning are clearly future work.
- [x] 2.3 Update Azure Functions adapter documentation to show manual wrappers as the current supported model and source-generated wrappers as deferred.
- [x] 2.4 Update Azure Functions adapter documentation to include the related transport, persistence, endpoint, and recoverability registrations needed by the documented setup path.
- [x] 2.5 Update SQL persistence documentation to instruct developers to apply every script in `src/MiniBus.Persistence.Sql/Schema/` in filename order.
- [x] 2.6 Update SQL persistence documentation to describe inbox/outbox behavior, outbox dispatch/drain behavior, deterministic outgoing ids, claim leases, cleanup, and idempotency expectations.
- [x] 2.7 Update SQL saga documentation to describe SQL-backed `ISagaPersistence`, saga schema setup, serialization expectations, completion behavior, and optimistic concurrency.
- [x] 2.8 Update Azure Storage persistence documentation to describe claim-check and audit blob setup as part of the current package surface.
- [x] 2.9 Update `MiniBus.Testing` documentation and root README links so testing helpers are part of the recommended developer workflow.

## 3. Package README Inclusion

- [x] 3.1 Ensure each distributable package has an appropriate README file for NuGet package display.
- [x] 3.2 Configure package README inclusion for every distributable package.
- [x] 3.3 Verify package README content names required companion packages and current limitations where relevant.

## 4. Backlog And Project Context

- [x] 4.1 Update `openspec/project.md` backlog/status to reflect package metadata and documentation polish work accurately.
- [x] 4.2 Keep source generator, analyzer, template, fuller sample, live Azure integration test, and infrastructure provisioning items open or deferred as appropriate.

## 5. Verification

- [x] 5.1 Run `dotnet build` for the solution.
- [ ] 5.2 Run `dotnet pack` in Release configuration for the intended package set or solution.
- [ ] 5.3 Verify generated package output contains only intended MiniBus packages.
- [ ] 5.4 Inspect generated packages for expected metadata and README inclusion.
- [ ] 5.5 Run `openspec validate nuget-ready-dx-polish --strict`.
