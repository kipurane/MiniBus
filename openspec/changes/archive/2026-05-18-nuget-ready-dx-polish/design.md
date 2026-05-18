## Context

MiniBus has moved past the initial runtime baseline: core processing, Azure Service Bus dispatch, Azure Functions processing, SQL inbox/outbox/saga persistence, Azure Storage claim-check/audit, observability, testing helpers, and acceptance coverage are present. The remaining near-term backlog is mostly developer experience: source generators, analyzers, templates, packaging, and documentation.

This change prepares the repository for early NuGet consumption without publishing packages or adding new runtime features. The current package surface is the set of projects under `src/`; tests and samples exist for validation and examples but are not intended to be packed.

## Goals / Non-Goals

**Goals:**

- Make all distributable MiniBus packages pack-ready with consistent metadata and build conventions.
- Keep shared package/build conventions centralized while allowing package-specific titles, descriptions, tags, and README files.
- Make test and sample projects explicitly non-packable.
- Refresh developer-facing documentation so it matches the implemented framework state.
- Provide a concise golden-path setup flow for Azure Functions, Azure Service Bus, SQL persistence, recoverability, and handler testing.
- Add local pack verification so package readiness is tested before the change is complete.

**Non-Goals:**

- Publishing packages to NuGet.
- Adding source generators, Roslyn analyzers, or templates.
- Adding live Azure infrastructure provisioning or live Azure Service Bus integration tests.
- Changing runtime behavior beyond build/package metadata.
- Introducing broad new public APIs to make documentation easier.

## Decisions

### Package set

Distributable packages are all current production/library projects under `src/`:

- `MiniBus.Core`
- `MiniBus.AzureServiceBus`
- `MiniBus.AzureFunctions`
- `MiniBus.Persistence.Sql`
- `MiniBus.Persistence.AzureStorage`
- `MiniBus.Testing`

Samples and tests remain non-packable. This keeps `dotnet pack` useful at solution level while avoiding accidental sample/test packages.

Alternative considered: only package `MiniBus.Core` first. That would avoid metadata work, but it would not match the current developer story because early adopters need the adapter, transport, persistence, and testing packages together.

### Metadata centralization

Use repository-level MSBuild configuration for shared package/build metadata and per-project metadata for package-specific values.

Central metadata should cover common authorship, repository URL/type, license expression or license file, package output conventions, deterministic build settings, symbol package settings, package validation settings when appropriate, and non-packable defaults where useful. Per-project files should define package id, title, description, tags, and package README selection.

Alternative considered: duplicate all metadata in each `.csproj`. That keeps each project self-contained, but it makes future versioning and publishing cleanup more error-prone.

### README and docs structure

Keep the root README as the main narrative and navigation entry, and keep package-specific setup details in package READMEs. Add or update a short golden-path section in the root README that links developers to package details instead of trying to make one giant document carry everything.

Package READMEs should be included in their NuGet packages through `PackageReadmeFile`. Each package README should accurately state what the package does, required companion packages, registration/setup basics, and current limitations.

Alternative considered: create a full `/docs` documentation tree now. That may become valuable later, especially with templates and analyzers, but for this polish pass it risks broad documentation churn before the publish surface is proven.

### SQL schema guidance

SQL documentation should consistently tell developers to apply every script in `src/MiniBus.Persistence.Sql/Schema/` in filename order. It should not mention only `001-inbox-outbox.sql` because SQL outbox migrations and SQL saga persistence now add additional scripts.

Alternative considered: document exact script names everywhere. That is useful for a migration history, but setup guidance should be resilient as new scripts are added.

### Pack verification

Implementation should verify at minimum:

- `dotnet build`
- `dotnet pack` for the solution or all intended package projects in Release configuration
- generated `.nupkg` files exist only for intended packages
- packages include expected package README files and metadata

NuGet publishing is intentionally excluded. Package contents can be inspected locally with standard archive tooling or a local package output directory.

## Risks / Trade-offs

- Package metadata can look complete without proving publish quality. Mitigation: verify actual `.nupkg` output and inspect package metadata/readme inclusion.
- Centralized MSBuild properties can accidentally affect tests or samples. Mitigation: explicitly mark tests and samples non-packable and confirm package output only includes intended projects.
- Documentation can drift again as source generators and analyzers arrive. Mitigation: document current limitations clearly and keep deferred features named as future work.
- Adding source link or symbol settings may introduce new build-package dependencies. Mitigation: prefer SDK-supported settings first, and only add build-time package references if they are necessary and private.

## Migration Plan

No runtime migration is required. Existing applications keep the same APIs and behavior.

Developers consuming local packages should rebuild and repack after the change. Applications using SQL persistence should follow the refreshed documentation and apply all packaged SQL schema scripts in filename order before enabling SQL persistence.

Rollback is straightforward: remove the packaging metadata/docs changes and return to direct project references.

## Open Questions

- What public repository URL should be used in package metadata if the repository is not yet public?
- Should a single version be centralized immediately, or should versioning stay explicit until the first real publishing workflow is designed?
- Should package validation be enabled in this change, or deferred until stable public baselines exist?
