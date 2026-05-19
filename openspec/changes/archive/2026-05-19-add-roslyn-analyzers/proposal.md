## Why

MiniBus now has enough runtime, persistence, observability, testing, and source-generation surface area that common setup mistakes can be caught before an application ever runs. A dedicated analyzer package gives developers fast feedback while keeping the runtime packages free of Roslyn dependencies.

## What Changes

- Add a new distributable analyzer package, `MiniBus.Analyzers`, for compile-time MiniBus guidance.
- Add high-signal diagnostics for handler shape, message contract, routing, Azure Functions registration, and feasible saga configuration mistakes.
- Define stable analyzer diagnostic IDs, severities, categories, and documentation so diagnostics can be understood, tested, and suppressed intentionally.
- Package the analyzers so consuming applications can reference them with analyzer-only package metadata such as `PrivateAssets="all"` and `OutputItemType="Analyzer"`.
- Add Roslyn analyzer tests that verify diagnostics and guard against false positives for common valid MiniBus usage.
- Document analyzer installation, diagnostic behavior, examples, suppression guidance, and known static-analysis limits.

## Capabilities

### New Capabilities

- `roslyn-analyzers`: Compile-time analyzer diagnostics for common MiniBus configuration, routing, handler, message contract, and feasible saga mistakes.

### Modified Capabilities

- None.

## Impact

- Adds a new `src/MiniBus.Analyzers` distributable project and a matching analyzer test project.
- Adds Roslyn analyzer dependencies only to the analyzer package and analyzer tests, not to MiniBus runtime packages.
- Extends package documentation and root guidance to describe analyzer installation and current limitations.
- Updates the project backlog to mark Roslyn analyzers complete when the implementation is finished.
