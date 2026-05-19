## Context

MiniBus already provides runtime contracts, Azure Functions processing, Azure Service Bus routing, SQL and Azure Storage persistence, observability hooks, testing helpers, and an Azure Functions source-generator package. The remaining developer-experience gap is earlier feedback for mistakes that are currently discovered through runtime exceptions, failed integration tests, or confusing generated/host behavior.

The existing source-generator package demonstrates the packaging shape for Roslyn-based assets: `netstandard2.0`, Roslyn dependencies marked private, build output packed under `analyzers/dotnet/cs`, and no runtime dependency leakage. The analyzer package should reuse that shape while keeping its diagnostic IDs separate from the Azure Functions wrapper generator IDs.

## Goals / Non-Goals

**Goals:**

- Add a `MiniBus.Analyzers` package that can be referenced as an analyzer-only development dependency.
- Catch common, high-confidence mistakes in handlers, message contracts, routing configuration, Azure Functions setup, and feasible saga setup.
- Use stable diagnostic descriptors with clear IDs, categories, severities, and documentation.
- Keep runtime packages free from Roslyn dependencies.
- Provide analyzer tests that prove diagnostics are emitted for invalid examples and avoided for valid MiniBus usage.
- Document installation, examples, suppression guidance, and known static-analysis limits.

**Non-Goals:**

- Do not add project templates, live Azure integration tests, automatic infrastructure provisioning, or publishing automation.
- Do not perform deep whole-program dependency-injection validation.
- Do not duplicate diagnostics already emitted by `MiniBus.AzureFunctions.SourceGenerators`.
- Do not require runtime behavior changes unless a small consistency fix is needed to align runtime validation and analyzer behavior.
- Do not include broad style analyzers or noisy recommendations in the first release.
- Do not add code fixes unless an implementation task identifies an obviously safe, low-risk fix.

## Decisions

### Create a separate `MiniBus.Analyzers` package

The analyzer package will live under `src/MiniBus.Analyzers` and pack its assembly into `analyzers/dotnet/cs`. It will target `netstandard2.0`, use Roslyn dependencies with `PrivateAssets="all"`, suppress package dependencies, and follow the central package metadata conventions already used by distributable projects.

Alternative considered: add analyzers to the existing Azure Functions source-generator package. That would couple general MiniBus diagnostics to Azure Functions wrapper generation and make installation confusing for developers who want one but not the other.

### Use a distinct diagnostic ID family

Analyzer diagnostics will use a MiniBus analyzer prefix distinct from existing source-generator IDs such as `MBFWR001`. A concise prefix such as `MBAN` gives stable IDs like `MBAN001` while leaving room for future analyzer families.

Alternative considered: reuse the source-generator prefix. That would make diagnostics harder to attribute and risks collisions as both packages evolve.

### Start with conservative diagnostics

The first release should prefer diagnostics that can be proven from symbols and visible syntax:

- Handler types implementing `IHandleMessages<TMessage>` must be concrete, non-generic where registered/discovered, and instantiable enough for normal DI registration.
- Message types used in MiniBus APIs must implement the expected marker contract.
- Constant empty route names are invalid.
- Generic route API misuse should be caught when the compiler permits a related non-generic form or when type symbols are visible.
- Missing route, missing `AddMiniBusAzureFunctions`, and saga-enable diagnostics should only fire when usage and configuration are visible in the same compilation with low false-positive risk.

Alternative considered: attempt full application validation. MiniBus configuration can be split across assemblies, environment-specific startup code, extension methods, and runtime conditions, so whole-program claims would be brittle and noisy.

### Keep source-generator diagnostics authoritative for wrapper declarations

The wrapper generator already validates required declaration values and duplicate function names. The analyzer package may recognize wrapper declarations for complementary checks, but it should not re-report the same invalid declaration diagnostics or reuse the same IDs.

Alternative considered: reimplement wrapper declaration validation in analyzers. That duplicates behavior, creates drift risk, and worsens the developer experience with duplicate errors.

### Test through small compiling fixtures

Analyzer tests should compile focused snippets with references to MiniBus projects and verify exact diagnostic IDs and locations. Each analyzer should include positive cases, negative cases, and at least one valid setup that resembles the sample application.

Alternative considered: rely only on solution-level builds. Build verification is necessary, but analyzer correctness needs targeted Roslyn tests for precise diagnostics and false-positive control.

## Risks / Trade-offs

- [Risk] Static analysis can produce false positives when configuration is split across methods or assemblies. -> Mitigation: only report cross-cutting configuration diagnostics when the relevant usage and configuration are visible and clearly contradictory or absent in a bounded scope.
- [Risk] Analyzer dependencies leak into runtime packages. -> Mitigation: keep analyzers in a dedicated project with private Roslyn references and package analyzer output explicitly.
- [Risk] Diagnostics become too noisy and developers suppress the package entirely. -> Mitigation: start with a small high-signal rule set, prefer warnings for incomplete knowledge, and document suppression guidance.
- [Risk] Analyzer behavior drifts from runtime validation. -> Mitigation: base symbol checks on MiniBus public contracts and add tests that mirror current valid and invalid runtime examples.
- [Risk] Saga and route completeness rules are tempting but hard to prove. -> Mitigation: make those diagnostics conditional on visible, reliable evidence and defer ambiguous rules.
