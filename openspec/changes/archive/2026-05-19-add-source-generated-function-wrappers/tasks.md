## 1. Project Setup

- [x] 1.1 Add a `MiniBus.AzureFunctions.SourceGenerators` project under `src/` with source-generator packaging conventions and Roslyn dependencies isolated from runtime packages.
- [x] 1.2 Add a source generator test project under `tests/` and include both projects in `MiniBus.sln`.
- [x] 1.3 Configure package metadata, README inclusion, and pack behavior for the new source generator package.

## 2. Declaration API

- [x] 2.1 Define queue trigger declaration attributes emitted into consuming compilations by the generator.
- [x] 2.2 Define topic/subscription trigger declaration attributes emitted into consuming compilations by the generator.
- [x] 2.3 Document the chosen attribute namespace, required constructor parameters, and any optional properties such as generated type name overrides.

## 3. Generator Implementation

- [x] 3.1 Implement declaration discovery for valid assembly-level queue trigger attributes.
- [x] 3.2 Implement declaration discovery for valid assembly-level topic/subscription trigger attributes.
- [x] 3.3 Generate deterministic wrapper classes that inject `MiniBusProcessor`.
- [x] 3.4 Generate queue trigger methods with `[Function]`, `[ServiceBusTrigger]`, `ServiceBusReceivedMessage`, `ServiceBusMessageActions`, and `CancellationToken`.
- [x] 3.5 Generate topic/subscription trigger methods with `[Function]`, `[ServiceBusTrigger]`, `ServiceBusReceivedMessage`, `ServiceBusMessageActions`, and `CancellationToken`.
- [x] 3.6 Ensure generated methods delegate to `MiniBusProcessor.ProcessAsync(ServiceBusReceivedMessage, ServiceBusMessageActions, CancellationToken)`.

## 4. Diagnostics

- [x] 4.1 Add diagnostics for empty or missing queue trigger function name, queue name, or connection setting name.
- [x] 4.2 Add diagnostics for empty or missing topic trigger function name, topic name, subscription name, or connection setting name.
- [x] 4.3 Add diagnostics for duplicate generated function names.
- [x] 4.4 Prevent wrapper source emission for declarations that have blocking diagnostics.

## 5. Tests

- [x] 5.1 Add Roslyn generator tests for valid queue trigger declarations and verify generated source shape.
- [x] 5.2 Add Roslyn generator tests for valid topic/subscription trigger declarations and verify generated source shape.
- [x] 5.3 Add generator tests for invalid declaration diagnostics.
- [x] 5.4 Add generator tests proving generated output is deterministic for repeated compilations.
- [x] 5.5 Add build or pack verification that runtime packages do not gain Roslyn source generator runtime dependencies.

## 6. Documentation and Sample Guidance

- [x] 6.1 Update root documentation to list generated wrappers as an optional developer-experience feature.
- [x] 6.2 Update `MiniBus.AzureFunctions` documentation to show generated wrapper declarations alongside the equivalent manual wrapper.
- [x] 6.3 Update sample documentation to explain when to use generated wrappers and when to keep manual wrappers.
- [x] 6.4 Add source generator package README content covering package reference, declarations, diagnostics, limitations, and manual-wrapper fallback.

## 7. Verification

- [x] 7.1 Run source generator tests.
- [x] 7.2 Run Azure Functions adapter tests.
- [x] 7.3 Run solution build.
- [ ] 7.4 Run package verification for distributable projects.
