## Context

`MiniBus.Core` currently exists only as a placeholder project with a single `Class1` type. The project architecture defines Phase 1 as a transport-agnostic core layer that provides message contracts, handler abstractions, serialization, routing, and handler invocation before any Azure Service Bus, Azure Functions, inbox/outbox, or saga work begins.

This change needs to establish a small, stable API surface that later packages can build on without introducing transport concerns into business handlers. The implementation must fit the current repository state: one class library already in `src/MiniBus.Core`, no test project yet under `tests`, and no existing OpenSpec capability files.

## Goals / Non-Goals

**Goals:**
- Define the foundational MiniBus contracts for messages, handlers, context, and serialization.
- Provide a core routing registry for command destinations and a handler registry for message-handler resolution.
- Define a message invocation flow that can deserialize a payload, resolve its message type, and execute the matching handlers using dependency injection.
- Keep the public API minimal so later transport and persistence packages depend on stable abstractions rather than internal implementation details.
- Establish unit-testable seams for serializer behavior, routing validation, handler discovery, and handler invocation.

**Non-Goals:**
- Azure Service Bus send, publish, or schedule implementations.
- Azure Functions trigger adapters or settlement logic.
- Pipeline behaviors, retries, inbox, outbox, saga persistence, or diagnostics beyond what is needed for the core contracts.
- Automatic assembly scanning across the full application domain; discovery can remain explicit and assembly-scoped for the MVP.

## Decisions

### 1. Keep the public surface centered on transport-agnostic contracts
The core package will expose `IMessage`, `ICommand`, `IEvent`, `IHandleMessages<TMessage>`, `MiniBusContext`, and `IMessageSerializer` as the primary extensibility points. These align directly with the architecture document and provide the minimum set of concepts required to write business handlers and later connect transports.

**Rationale:** Downstream packages such as Azure Service Bus and Azure Functions need stable abstractions now, but transport-specific APIs would leak infrastructure concerns into handlers if introduced too early.

**Alternatives considered:**
- Expose transport-facing abstractions immediately: rejected because it violates the thin-adapter architecture.
- Keep everything internal until more features exist: rejected because transport and testing packages need a contract to target.

### 2. Use `System.Text.Json` as the default serializer behind `IMessageSerializer`
The core capability will define `IMessageSerializer` and provide a `System.Text.Json` implementation that serializes arbitrary message instances to UTF-8 JSON and deserializes them back using an explicit `Type`.

**Rationale:** `System.Text.Json` is the documented default for MiniBus, ships with .NET, and keeps MVP dependencies low.

**Alternatives considered:**
- Newtonsoft.Json: rejected because it adds a dependency without a documented need.
- Hard-code `System.Text.Json` without an abstraction: rejected because future transports and tests need a serializer seam.

### 3. Model routing as an explicit command route registry
The core layer will include a routing registry that maps command message types to logical destinations and validates uniqueness and missing routes. Event publishing topology is intentionally deferred to later transport work, but the design leaves room for event topic mapping later.

**Rationale:** The project guidance prefers explicit routes and requires fast failure for missing or conflicting command mappings.

**Alternatives considered:**
- Convention-based routing by type name: rejected because the project explicitly favors explicit routes.
- Include full event routing now: rejected to keep Phase 1 focused on the minimum command path needed by handlers and tests.

### 4. Resolve handlers through a registry that can be populated from assemblies and invoked via DI
The core capability will provide a handler registry that identifies concrete implementations of `IHandleMessages<TMessage>` and resolves them via the application service provider at invocation time. Invocation will support one message being handled by zero, one, or multiple handlers depending on registration.

**Rationale:** Handler discovery and invocation are called out as Phase 1 requirements, and resolving through DI keeps handlers testable and consistent with .NET application patterns.

**Alternatives considered:**
- Instantiate handlers with reflection only: rejected because it bypasses constructor injection.
- Depend on a specific DI container: rejected because `Microsoft.Extensions.DependencyInjection` abstractions are sufficient.

### 5. Separate contract types from orchestration helpers
The core library should distinguish between public contracts and internal orchestration components such as message type resolution, invocation coordination, and registry implementations. Public APIs remain small, while internal classes can evolve as later transport and persistence features are added.

**Rationale:** The architecture explicitly asks for minimal stable public APIs and internal flexibility.

**Alternatives considered:**
- Make registries and invokers fully public: rejected because it freezes implementation choices too early.
- Hide all orchestration internals with no test seam: rejected because unit tests still need to verify observable behavior.

## Risks / Trade-offs

- **[Risk] Early public APIs may prove too narrow for later transport work** → **Mitigation:** keep the contract set minimal but aligned to the architecture document, and keep orchestration helpers internal until broader needs are validated.
- **[Risk] Assembly scanning can become ambiguous or expensive as the solution grows** → **Mitigation:** constrain MVP discovery to explicit assemblies provided during registration and make discovery behavior deterministic.
- **[Risk] Command-only routing in Phase 1 may be mistaken for full transport routing support** → **Mitigation:** document event publishing and transport mapping as explicitly out of scope in proposal, design, and specs.
- **[Risk] Multiple handlers for a single message type introduce ordering ambiguity** → **Mitigation:** define that all registered handlers are invoked, but do not guarantee an application-visible ordering contract in the MVP.

## Migration Plan

1. Replace the placeholder `Class1` in `src/MiniBus.Core` with the new contracts and internal core runtime types.
2. Add a `tests/MiniBus.Core.Tests` project and include it in `MiniBus.sln`.
3. Implement serializer, routing, handler discovery, and invocation in small increments with unit tests validating each behavior.
4. Keep the package transport-agnostic so later changes can layer Azure Service Bus and Azure Functions support without reworking the core contracts.

Rollback is low risk because this is the first substantive framework capability; reverting the change removes only newly introduced artifacts.

## Open Questions

- Should the first DI integration expose extension methods such as `AddMiniBus(...)` immediately, or should service registration stay internal until more options exist?
- Should handler invocation allow multiple handlers for commands in the MVP, or should command semantics become single-owner only at invocation time while events later support fan-out?
- Should message type resolution rely solely on explicit registration in Phase 1, or should it include assembly-based lookup for deserialization support?

