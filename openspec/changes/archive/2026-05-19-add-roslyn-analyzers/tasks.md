## 1. Project Setup

- [x] 1.1 Add a `MiniBus.Analyzers` project under `src/` using analyzer packaging conventions.
- [x] 1.2 Add a `MiniBus.Analyzers.Tests` project under `tests/` with Roslyn analyzer test dependencies.
- [x] 1.3 Include the analyzer and analyzer test projects in `MiniBus.sln`.
- [x] 1.4 Configure analyzer package metadata, README inclusion, private Roslyn dependencies, and `analyzers/dotnet/cs` package output.

## 2. Diagnostic Infrastructure

- [x] 2.1 Define stable MiniBus analyzer diagnostic descriptors with a prefix distinct from source-generator diagnostics.
- [x] 2.2 Add shared symbol helpers for identifying MiniBus message contracts, handlers, route APIs, Azure Functions registration APIs, and saga configuration APIs.
- [x] 2.3 Add analyzer test helpers that compile snippets against MiniBus references and verify diagnostic IDs, severities, and locations.

## 3. Handler and Message Contract Analyzers

- [x] 3.1 Implement diagnostics for abstract MiniBus handler types.
- [x] 3.2 Implement diagnostics for open generic handler types in visible handler registration or discovery contexts.
- [x] 3.3 Implement diagnostics for ambiguous message contracts that implement both `ICommand` and `IEvent`.
- [x] 3.4 Implement high-confidence message-contract diagnostics for visible MiniBus API misuse not already covered by compiler errors.
- [x] 3.5 Add positive and negative analyzer tests for handler and message contract diagnostics.

## 4. Routing Analyzers

- [x] 4.1 Implement diagnostics for constant empty or whitespace route destinations in `MapCommand`, `MapEvent`, and `MapScheduledMessage`.
- [x] 4.2 Implement conservative missing-route diagnostics for visible command send, event publish, and scheduled message usage when matching route configuration is visible and incomplete.
- [x] 4.3 Ensure dynamic or externally split route configuration does not produce route completeness diagnostics.
- [x] 4.4 Add positive and negative analyzer tests for routing diagnostics and false-positive guard cases.

## 5. Azure Functions and Saga Analyzers

- [x] 5.1 Implement conservative Azure Functions setup diagnostics for visible MiniBus processor usage without visible `AddMiniBusAzureFunctions` registration.
- [x] 5.2 Ensure analyzers do not duplicate diagnostics already emitted by `MiniBus.AzureFunctions.SourceGenerators`.
- [x] 5.3 Implement saga diagnostics only for visible saga or timeout usage with visibly disabled saga processing.
- [x] 5.4 Add positive and negative analyzer tests for Azure Functions setup, source-generator coexistence, and saga diagnostics.

## 6. Documentation and Package Guidance

- [x] 6.1 Add analyzer package README content covering installation, diagnostic IDs, examples, suppression guidance, and static-analysis limitations.
- [x] 6.2 Update root documentation to list `MiniBus.Analyzers` as optional developer tooling.
- [x] 6.3 Update package readiness or developer-experience documentation only where needed to include the analyzer package in local pack verification guidance.
- [x] 6.4 Update the project backlog to mark Roslyn analyzers complete once implementation and verification are done.

## 7. Verification

- [x] 7.1 Run analyzer tests.
- [x] 7.2 Run source generator tests to verify diagnostic coexistence.
- [x] 7.3 Run solution build.
- [x] 7.4 Run package verification for distributable projects and inspect that runtime packages do not contain Roslyn analyzer dependencies.
- [x] 7.5 Run `openspec validate add-roslyn-analyzers --strict`.
