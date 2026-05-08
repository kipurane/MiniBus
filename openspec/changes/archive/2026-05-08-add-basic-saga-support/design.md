## Context

MiniBus currently provides message contracts, handler invocation, Azure Service Bus transport, Azure Functions processing, and recoverability. The architecture document identifies saga/state-machine support as a later MVP phase for long-running workflows with persisted state.

There is no current `MiniBus.Persistence.Sql` package or SQL inbox/outbox capability in the repository. This change should therefore define the core saga model and persistence abstraction without pretending a SQL transaction boundary already exists. The design should leave a clean place for SQL saga persistence to participate in a future persistence change.

## Goals / Non-Goals

**Goals:**
- Add transport-independent saga abstractions to `MiniBus.Core`.
- Define saga data identity, correlation, and completion behavior.
- Require explicit correlation mappings for starting and continuing messages.
- Add saga persistence abstraction for load/create/save/complete operations.
- Add saga invocation behavior that loads state before saga handling and saves state after successful saga handling.
- Create new saga state for configured starting messages.
- Ignore or fail clearly when a non-starting message cannot be correlated to saga state.
- Preserve MiniBus message headers, correlation id, and causation id through saga handler execution and outgoing operations.
- Add focused unit tests and a sample saga.

**Non-Goals:**
- Advanced saga DSL.
- Timeout-specific saga APIs.
- Service Bus sessions.
- Source generation.
- Visual workflow tooling.
- SQL saga persistence, unless a SQL persistence package exists before implementation begins.
- Azure Storage saga persistence.
- Distributed transaction support beyond an existing persistence boundary.

## Decisions

### 1. Keep saga contracts in core and persistence implementations outside core

`MiniBus.Core` will own saga contracts, correlation mapping, saga metadata, and the persistence abstraction. Database-specific implementations such as SQL will live outside core.

**Rationale:** Business sagas should be testable and independent from Azure Functions, Service Bus, SQL, or any future storage provider.

**Alternatives considered:**
- Put saga support directly in the Azure Functions adapter: rejected because saga behavior is not host-specific.
- Implement SQL persistence now: rejected because the repo has no SQL persistence package or transaction abstraction yet.

### 2. Use explicit correlation mappings

Saga correlation will be configured explicitly, for example by mapping a message property to a saga data property or by registering a finder. Starting messages must also be explicit.

**Rationale:** The project favors explicit routing and fast failure over convention-only behavior. Sagas are sensitive to accidental correlation mistakes, so mappings should be deliberate.

**Alternatives considered:**
- Infer correlation from property names such as `OrderId`: rejected because it is convenient but too implicit for the first saga capability.
- Require every saga to write a custom finder: rejected because simple property-to-property mapping should be easy and testable.

### 3. Separate regular handlers from saga handlers

Saga handlers will use saga-specific contracts so MiniBus can identify saga types, attach saga data, and persist lifecycle changes. Regular `IHandleMessages<TMessage>` handlers continue to work unchanged.

**Rationale:** Existing handler invocation should remain stable, while saga handlers need lifecycle semantics that ordinary handlers do not have.

**Alternatives considered:**
- Treat any handler with injected saga data as a saga: rejected because it hides lifecycle behavior in dependency injection.
- Make every handler saga-capable: rejected because most handlers do not need persisted workflow state.

### 4. Persist only after successful saga handling

MiniBus will load or create saga data before invoking the saga handler. It will save state only after the handler completes successfully. If the handler throws, the original exception flows through recoverability and state is not saved by the saga behavior.

**Rationale:** This aligns with existing processing and recoverability behavior. Failed concurrent updates or handler failures should retry through recoverability rather than committing partial saga state.

**Alternatives considered:**
- Save before handler invocation: rejected because the handler may fail and leave misleading state.
- Swallow persistence failures: rejected because persistence is part of processing success.

### 5. Use optimistic-concurrency-ready persistence contracts

The persistence abstraction should carry enough metadata for future stores to detect concurrent updates, such as a version token or revision value. The first implementation may use in-memory/fake stores in tests, but the contract should not block SQL rowversion-based implementations later.

**Rationale:** The architecture document explicitly calls for optimistic concurrency. Even without SQL persistence in this change, the core abstraction should not paint future storage into a corner.

**Alternatives considered:**
- Ignore concurrency until SQL exists: rejected because adding version concepts later would churn the public abstraction.

### 6. Completed sagas are terminal

Saga data can be marked complete during handling. Completed saga state is persisted as completed or removed according to the persistence implementation. Later messages that correlate to completed saga state must not invoke the saga again.

**Rationale:** Long-running workflow state needs a clear end. A completed saga should not be accidentally rehydrated and mutated by late messages.

**Alternatives considered:**
- Delete completed saga state unconditionally in core: rejected because retention/audit behavior belongs to persistence policy.

### 7. Integrate through core invocation seams used by Azure Functions

The Azure Functions adapter should continue to deserialize and create `MiniBusContext` as it does today, then invoke core processing behavior that can include saga handlers. It must not expose `ServiceBusReceivedMessage`, `ServiceBusMessageActions`, or SQL types to saga code.

**Rationale:** Azure Functions remains a thin adapter, and saga behavior stays in the framework core.

**Alternatives considered:**
- Add saga-specific Azure Function wrappers: rejected as source-generated wrappers and host-specific saga APIs are out of scope.

## Risks / Trade-offs

- **[Risk] Adding saga abstractions before SQL persistence may leave no production store.** -> **Mitigation:** provide a persistence abstraction and tests, document that production SQL implementation follows once persistence exists.
- **[Risk] Correlation mapping API can become too broad too early.** -> **Mitigation:** support explicit property mapping plus a finder abstraction, and defer advanced DSL conveniences.
- **[Risk] Saga invocation may complicate existing handler invocation.** -> **Mitigation:** keep regular handler invocation unchanged and add a saga-specific behavior path with focused tests.
- **[Risk] Completed saga retention semantics may vary by store.** -> **Mitigation:** define completed saga behavior at the framework level while allowing persistence implementations to choose mark-complete or delete storage policy.

## Migration Plan

1. Add saga contracts, saga data model, correlation mapping, and persistence abstraction to `MiniBus.Core`.
2. Add saga handler discovery/registration or explicit registration support.
3. Implement core saga invocation behavior for load/create/save/complete.
4. Integrate the Azure Functions processor with the core saga invocation behavior while keeping handlers transport-independent.
5. Add tests for saga start, correlation to existing state, completion, missing state, failed handlers, and header/context preservation.
6. Add documentation and a sample saga.
7. Build the solution and run all MiniBus test projects.

Rollback is manageable before consumers adopt the new APIs: remove saga contracts, invocation behavior, tests, and samples.

## Open Questions

- Should missing saga state for a non-starting message be ignored, dead-lettered, or treated as a recoverable processing failure?
- Should completed saga state be deleted by default or retained with a completed flag?
- Should the first public API expose a fluent mapper, attribute-based mapping, or only code-based mapping callbacks?
- Should saga handlers implement separate interfaces for starting and continuing messages, or should this be metadata on the correlation mapping?
