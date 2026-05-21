## 1. Template Package And Metadata

- [x] 1.1 Add the v1 MiniBus template package/project structure and `dotnet new` metadata for the focused Azure Functions starter short name.
- [x] 1.2 Configure the template package to emit consumer-style package references for the required MiniBus runtime packages and default analyzer dependency.
- [x] 1.3 Add repository-facing template packaging or installability plumbing needed to instantiate the template from locally built artifacts.

## 2. Generated Azure Functions Starter

- [x] 2.1 Author the generated isolated-worker Function App host shape, including host entry files and local configuration placeholders that build without live Azure resources.
- [x] 2.2 Add starter message contracts, one MiniBus handler, and the default thin manual Service Bus queue-trigger wrapper that delegates to `MiniBusProcessor`.
- [x] 2.3 Add MiniBus registration, serializer and handler registration, Azure Service Bus route/dispatcher registration, recoverability defaults, and explicit starter route placeholders in the generated project.
- [x] 2.4 Add generated-project documentation that explains the starter flow, Service Bus settings/topology ownership, source-generated wrapper follow-up, SQL persistence follow-up, and why saga code is not part of the v1 starter.

## 3. Verification Coverage

- [x] 3.1 Add automated verification that packs or installs the template for `dotnet new` from repository-local artifacts and generates the default starter project into a scratch location.
- [x] 3.2 Add verification for generated output shape and dependencies, including the expected host files, manual wrapper path, documentation/configuration placeholders, analyzer inclusion, and omission of default source-generator, SQL persistence, and saga wiring.
- [x] 3.3 Build the default generated project against locally packed MiniBus packages without requiring live Azure Service Bus resources.
- [x] 3.4 Run the targeted template verification together with the relevant repository build or pack checks needed to prove the new template path is distributable.

## 4. Developer Guidance

- [x] 4.1 Update the root README golden path with template installation/invocation guidance while preserving the manual setup path and current deferred infrastructure boundaries.
- [x] 4.2 Update Function App sample documentation as needed to point at the reusable host template while keeping the sample focused on the registration and sample-code reference shape.
- [x] 4.3 Mark the active OpenSpec project backlog entry for project templates complete once the template and verification path land.
