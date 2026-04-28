## Why

MiniBus currently contains only a stub `MiniBus.Core` project, so there is no framework-level contract for messages, handlers, serialization, or routing. Establishing the core message-processing capability now creates the foundation for all later transport, Azure Functions, and persistence work while keeping those concerns out of the initial MVP scope.

## What Changes

- Introduce the first MiniBus core capability for message contracts, handler abstractions, serialization, routing, and handler invocation.
- Define a minimal public API in `MiniBus.Core` for `IMessage`, `ICommand`, `IEvent`, `IHandleMessages<TMessage>`, `MiniBusContext`, and `IMessageSerializer`.
- Add a default `System.Text.Json` serializer and registries for command routing and handler resolution.
- Define the runtime behavior for discovering handlers, resolving a message type, and invoking matching handlers independent of Azure Service Bus or Azure Functions.
- Add unit-test coverage and solution structure needed to validate core message-processing behavior.

## Capabilities

### New Capabilities
- `minibus-core`: Core message-processing abstractions and behavior for message contracts, serialization, routing, handler discovery, and handler invocation.

### Modified Capabilities
- None.

## Impact

- Affected code: `src/MiniBus.Core`, `tests/MiniBus.Core.Tests`, and `MiniBus.sln`.
- Public APIs: introduces the first stable MiniBus contracts that downstream transport and application packages will depend on.
- Dependencies: relies on built-in .NET libraries, primarily `System.Text.Json` and dependency injection abstractions already standard for .NET projects.
- Systems: no transport, Azure Functions, retry, outbox, inbox, or saga behavior is included in this change.

