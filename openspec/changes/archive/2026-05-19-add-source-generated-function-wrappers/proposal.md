## Why

MiniBus manual Azure Functions wrappers are intentionally explicit and supported, but every endpoint still has to repeat the same thin `ServiceBusTrigger` class that delegates to `MiniBusProcessor`. Source-generated wrappers are the next developer-experience step because the runtime, persistence, observability, testing, and package-readiness baseline is already in place.

## What Changes

- Add a narrow v1 source generator package for Azure Functions isolated worker Service Bus trigger wrappers, likely `MiniBus.AzureFunctions.SourceGenerators`.
- Introduce a small MiniBus-owned declaration API that lets consuming applications describe generated Service Bus trigger wrappers.
- Generate deterministic thin wrapper classes/methods with `[Function]` and `[ServiceBusTrigger]` attributes.
- Generate wrappers that inject `MiniBusProcessor` and delegate to `ProcessAsync(ServiceBusReceivedMessage, ServiceBusMessageActions, CancellationToken)`.
- Support queue triggers and topic/subscription triggers.
- Emit focused compile-time diagnostics for invalid wrapper declarations.
- Keep manual wrappers fully supported and documented.
- Add generator tests that compile sample declarations and verify generated source shape.
- Update documentation and sample guidance to show generated wrappers as an optional path alongside manual wrappers.

## Capabilities

### New Capabilities
- `source-generated-function-wrappers`: Defines the declaration model, generated wrapper behavior, diagnostics, tests, and documentation for MiniBus Azure Functions source-generated Service Bus trigger wrappers.

### Modified Capabilities
- `azure-functions-adapter`: Documents that manual wrappers remain supported while generated wrappers become an optional integration model that delegates to the existing processor.
- `package-readiness`: Adds package-readiness expectations for the new source generator package if it is introduced as a distributable project.

## Impact

- Adds a source generator project and test project to the solution.
- Adds public declaration attributes or marker types consumed by the generator.
- Adds analyzer/source-generator package references to sample or test applications.
- Adds compile-time dependencies on Roslyn generator APIs in the generator package only.
- Uses Azure Functions worker and Service Bus trigger attribute names in generated code while preserving the runtime package boundaries.
- Updates `README.md`, `src/MiniBus.AzureFunctions/README.md`, and sample documentation once implemented.
