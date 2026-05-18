## Why

MiniBus now has the core runtime, production persistence, observability, testing helpers, and reference coverage needed for early adopters to try it, but the package and documentation surface still feels pre-publish. This change makes the repository NuGet-ready before adding source generators, analyzers, or templates, so developers can understand the current golden path without being tripped by stale docs or inconsistent package metadata.

## What Changes

- Add centralized package/build metadata conventions for distributable MiniBus projects.
- Mark test and sample projects as non-packable unless they are intentionally published later.
- Add consistent NuGet metadata to distributable packages, including descriptions, repository metadata, license metadata, tags, package README inclusion, deterministic build settings, and CI-friendly pack settings.
- Update root and package documentation so current capabilities are accurate, including SQL schema script ordering, SQL saga persistence, outbox dispatch, Azure Storage claim-check/audit, observability, and `MiniBus.Testing`.
- Add a concise golden-path setup guide for Azure Functions, Azure Service Bus, SQL persistence, recoverability, and testing.
- Document current limitations and explicitly defer source generators, Roslyn analyzers, project templates, live Azure integration tests, publishing, and automatic Azure infrastructure provisioning.
- Add pack verification tasks so early NuGet readiness is checked locally before implementation is considered complete.

## Capabilities

### New Capabilities

- `package-readiness`: Defines requirements for MiniBus package metadata, pack readiness, documentation accuracy, golden-path setup guidance, and explicit publish limitations.

### Modified Capabilities

- `azure-functions-adapter`: Clarifies that Azure Functions documentation must describe manual wrapper setup accurately while source-generated wrappers remain deferred.
- `sql-inbox-outbox`: Clarifies that SQL documentation must direct developers to apply all schema scripts in filename order rather than referencing only a single inbox/outbox script.
- `sql-saga-persistence`: Clarifies that SQL documentation must include saga persistence setup and schema guidance now that SQL saga persistence exists.
- `minibus-testing`: Clarifies that documentation must show the testing package as part of the recommended developer workflow.

## Impact

- Affected files include repository-level MSBuild props/targets, distributable `src/*/*.csproj` package metadata, root and package README files, and OpenSpec project/backlog status.
- Runtime behavior should not change except where build metadata affects packaging.
- No NuGet publishing, source generator, analyzer, template, live Azure provisioning, or broad public API work is included.
