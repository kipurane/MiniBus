## Why

MiniBus can process messages, dispatch through Azure Service Bus, and apply basic recoverability, but it still lacks a framework-level model for long-running workflows that need persisted state across multiple messages. Without saga support, applications must hand-roll correlation, state loading, completion, and persistence around each handler.

This change introduces a minimal saga capability that keeps saga handlers transport-independent while giving MiniBus a clear model for starting, correlating, loading, saving, and completing workflow state.

## What Changes

- Add transport-independent saga abstractions to `MiniBus.Core`.
- Add saga data identity and completion concepts.
- Add explicit saga correlation mapping for starting and continuing messages.
- Add saga finder/correlation behavior that resolves a saga id or correlation id from incoming message data.
- Add a saga persistence abstraction for loading, creating, saving, and completing saga state.
- Integrate saga state loading before handler invocation and saving after successful handling.
- Support creating saga state when a configured starting message arrives.
- Support marking saga state as completed.
- Preserve MiniBus headers, correlation, and causation through saga message handling.
- Add tests for saga start behavior, existing saga loading, and completed saga behavior.
- Add documentation and a sample saga.

## Capabilities

### New Capabilities
- `basic-saga-support`: Minimal saga/state-machine support for long-running workflows with explicit correlation and persistence abstractions.

### Modified Capabilities
- `minibus-core`: Adds transport-independent saga contracts, correlation mapping, and saga invocation/persistence abstractions.
- `azure-functions-adapter`: Uses the core saga invocation behavior during message processing without exposing Azure Functions or Service Bus types to sagas.

## Impact

- Affected code: `src/MiniBus.Core`, `src/MiniBus.AzureFunctions`, tests, and samples/docs.
- Public APIs: introduces saga contracts such as saga data, saga base/handler abstractions, correlation mapping, saga persistence, and completion markers.
- Dependencies: no new external runtime dependency is required for the core saga model.
- Systems: SQL saga persistence is not implemented in this change because no SQL persistence package exists yet; the new persistence abstraction is designed for a later `MiniBus.Persistence.Sql` implementation.
