## 1. Solution and project setup

- [x] 1.1 Replace the placeholder `Class1` implementation in `src/MiniBus.Core` with the initial folder and type structure for contracts, context, serialization, routing, and handler execution.
- [x] 1.2 Create a `tests/MiniBus.Core.Tests` project, add a project reference to `MiniBus.Core`, and include the test project in `MiniBus.sln` under the `tests` solution folder.

## 2. Core contracts and serialization

- [x] 2.1 Implement the public message contracts `IMessage`, `ICommand`, `IEvent`, `IHandleMessages<TMessage>`, and the `MiniBusContext` abstraction in `src/MiniBus.Core`.
- [x] 2.2 Implement `IMessageSerializer` and a default `System.Text.Json` serializer that round-trips message instances using an explicit message `Type`.
- [x] 2.3 Add unit tests covering message contract usage and serializer round-trip behavior.

## 3. Routing and handler registration

- [x] 3.1 Implement an explicit command routing registry that stores type-to-destination mappings and rejects missing or conflicting routes.
- [x] 3.2 Implement handler discovery that scans configured assemblies for concrete `IHandleMessages<TMessage>` implementations and produces handler registrations suitable for DI.
- [x] 3.3 Add unit tests for command routing success, missing-route failure, conflicting-route failure, handler discovery, and ignored non-handler types.

## 4. Handler invocation flow

- [x] 4.1 Implement core message invocation components that resolve matching handlers from the service provider and invoke them asynchronously with the message instance, `MiniBusContext`, and `CancellationToken`.
- [x] 4.2 Ensure the invocation flow supports zero or more matching handlers without leaking transport-specific concerns into the core package.
- [x] 4.3 Add unit tests for invoking one handler, invoking multiple handlers, and completing successfully when no handlers are registered.

## 5. Readiness and documentation

- [x] 5.1 Review public APIs to keep only the intended transport-agnostic contracts public and keep orchestration helpers internal unless required by tests.
- [x] 5.2 Build the solution and run the `MiniBus.Core.Tests` test suite to verify the new capability end to end.

