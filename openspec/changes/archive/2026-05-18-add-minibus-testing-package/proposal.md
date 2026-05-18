## Why

MiniBus handlers and saga handlers are testable in principle because they depend on `MiniBusContext` instead of Azure Functions or transport types, but application developers still need to hand-roll their own context doubles to assert outgoing work. The codebase already repeats private `RecordingMiniBusContext` implementations, so the next developer experience slice should turn that pattern into a small supported testing package.

## What Changes

- Add a new `MiniBus.Testing` package focused on handler-facing unit testing helpers.
- Add `TestableMiniBusContext`, deriving from `MiniBusContext`, with configurable inbound metadata and headers.
- Capture handler-requested send, publish, and schedule operations while preserving message objects, message types, headers where relevant, and due times.
- Add dependency-light typed query helpers for captured operations when they stay framework-agnostic.
- Add `MiniBus.Testing.Tests` coverage and include the new projects in the solution.
- Add package-level documentation showing direct handler unit testing with `TestableMiniBusContext`.
- Keep processor/integration harnesses, live Azure Service Bus testing, analyzers, source generators, templates, and NuGet publishing metadata out of scope.

## Capabilities

### New Capabilities

- `minibus-testing`: Developer testing helpers for unit testing handlers and saga handlers without transport, host, storage, or test-framework dependencies.

### Modified Capabilities

None.

## Impact

- New project: `src/MiniBus.Testing`.
- New test project: `tests/MiniBus.Testing.Tests`.
- Solution updates for both projects.
- Documentation for the new testing package.
- No expected runtime behavior, production API, storage schema, transport, Azure Functions, SQL, Azure Storage, or observability changes.
