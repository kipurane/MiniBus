# package-readiness Specification

## Purpose
TBD - created by archiving change nuget-ready-dx-polish. Update Purpose after archive.
## Requirements
### Requirement: Distributable projects define consistent NuGet metadata
MiniBus SHALL provide consistent NuGet package metadata for each distributable project under `src/`.

#### Scenario: Package project is packed
- **WHEN** a distributable MiniBus project is packed
- **THEN** the generated package contains consistent metadata for package id, title, description, authors, license, repository, tags, readme file, and build provenance where configured

#### Scenario: Shared metadata is maintained centrally
- **WHEN** common package metadata or build conventions are updated
- **THEN** the update can be made in a central MSBuild configuration without duplicating the same values across every package project

#### Scenario: Package-specific metadata remains distinct
- **WHEN** a package has a package-specific title, description, tags, or README
- **THEN** that metadata is defined by the package project or an equivalent package-specific configuration

### Requirement: Non-distributable projects do not produce NuGet packages
MiniBus SHALL prevent test and sample projects from producing NuGet packages as part of the normal pack workflow.

#### Scenario: Solution pack runs
- **WHEN** the solution or repository pack workflow is run
- **THEN** packages are produced only for intended distributable MiniBus projects

#### Scenario: Test project is packed accidentally
- **WHEN** a test project participates in a solution-level pack command
- **THEN** it is marked non-packable and does not produce a `.nupkg`

#### Scenario: Sample project is packed accidentally
- **WHEN** a sample project participates in a solution-level pack command
- **THEN** it is marked non-packable and does not produce a `.nupkg`

### Requirement: Package documentation is accurate and included
MiniBus SHALL include accurate package README documentation in each distributable NuGet package.

#### Scenario: Package is inspected
- **WHEN** a generated MiniBus package is inspected
- **THEN** it includes the configured package README file

#### Scenario: Developer reads package documentation
- **WHEN** a developer reads a package README
- **THEN** it accurately describes the package purpose, required companion packages, setup basics, and current limitations

### Requirement: Root documentation provides a golden path
MiniBus SHALL provide a concise root documentation path for setting up an early Azure Functions application with Azure Service Bus, SQL persistence, recoverability, and handler testing.

#### Scenario: Developer starts from root README
- **WHEN** a developer reads the root README
- **THEN** it directs them through the current recommended setup flow for core contracts, handler registration, manual Azure Functions wrappers, Azure Service Bus routes, SQL persistence scripts, recoverability, outbox dispatch, observability, and testing

#### Scenario: Deferred tooling is described
- **WHEN** a developer reads the setup documentation
- **THEN** it clearly states that source-generated wrappers are optional tooling and that broader Roslyn analyzers, project templates, live Azure integration tests, and automatic infrastructure provisioning are future work

### Requirement: Package readiness is locally verifiable
MiniBus SHALL define local verification steps for package readiness before implementation is considered complete.

#### Scenario: Build verification runs
- **WHEN** package readiness verification runs
- **THEN** the repository builds successfully

#### Scenario: Pack verification runs
- **WHEN** package readiness verification runs
- **THEN** intended distributable packages are produced successfully in Release configuration

#### Scenario: Package output is inspected
- **WHEN** generated package artifacts are inspected
- **THEN** the output contains only intended MiniBus packages and includes expected README and metadata

### Requirement: Source generator package is distributable
MiniBus SHALL define package metadata and package documentation for the Azure Functions source generator package when it is introduced as a distributable package.

#### Scenario: Source generator package is packed
- **WHEN** the Azure Functions source generator project is packed
- **THEN** the generated package contains package metadata, README content, repository metadata, license metadata, and tags consistent with the other distributable MiniBus packages

#### Scenario: Source generator package is consumed
- **WHEN** a developer reads the source generator package README
- **THEN** it explains how to reference the package, declare generated wrappers, and keep manual wrappers as a supported alternative

#### Scenario: Runtime packages are packed
- **WHEN** MiniBus runtime packages are packed
- **THEN** they do not include Roslyn source generator implementation dependencies as runtime dependencies
