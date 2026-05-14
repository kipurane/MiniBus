## Context

`MiniBusProcessor` is currently the Azure Functions entry point and the orchestration engine for processing. It adapts `ServiceBusReceivedMessage` into headers, resolves and deserializes the message type, checks SQL inbox state, creates the handler-facing context, invokes handlers and sagas, captures outbox operations, commits persistence, executes recoverability decisions, schedules delayed retries, and settles the Service Bus message.

That shape works for the current feature set, but it makes new cross-cutting behavior like observability, richer recoverability, and SQL saga persistence harder to add without growing one large method. The project needs explicit internal processing seams while keeping public Azure Functions APIs and business handler APIs stable.

## Goals / Non-Goals

**Goals:**

- Refactor processing into ordered internal pipeline behaviors.
- Introduce a shared processing context that carries all state currently passed through local variables.
- Keep `MiniBusProcessor` public overloads source compatible.
- Preserve existing behavior for no-settlement and settlement-enabled processing.
- Make duplicate inbox checks, outbox capture, saga invocation, recoverability, delayed retry scheduling, and settlement decisions testable at behavior boundaries.
- Keep the implementation small enough to remain understandable.

**Non-Goals:**

- Public middleware/plugin extensibility.
- New OpenTelemetry, logging, or metrics behavior.
- SQL saga persistence.
- Azure Storage persistence.
- New retry policies or exception classification.
- Broad replacement of `MiniBusProcessorOptions` with a new public `MiniBusOptions` object.

## Decisions

### Build an internal pipeline first

The first pipeline implementation should be internal to the Azure Functions adapter unless a specific type is clearly transport-neutral. The public surface remains `MiniBusProcessor.ProcessAsync(...)`, and business handlers continue to see only `MiniBusContext`.

Alternative considered: expose public middleware now. That would create a public contract before the framework has enough real extension cases to know which abstractions should be stable.

### Use an explicit mutable processing context

Introduce a processing context object that carries the received Service Bus message, settlement actions when present, endpoint name, headers, resolved message type, deserialized message, handler-facing `MiniBusContext`, persistence session/inbox state, outbox collector/operations, recoverability decision data, and settlement decision. Behaviors can read and write named properties instead of passing long parameter lists.

Alternative considered: keep passing small immutable records between steps. That is elegant for pure transformations, but current processing includes short-circuiting, retries, async resources, and settlement decisions, so one explicit context is simpler and easier to inspect in tests.

### Keep recoverability as an outer behavior

Recoverability should wrap the core processing pipeline for settlement-enabled processing, preserving immediate retry loops and delayed retry/dead-letter decisions. The no-settlement overload should use the same core pipeline but allow failures to propagate without settlement.

Alternative considered: make every behavior handle recoverability individually. That would duplicate decision logic and make failure behavior inconsistent.

### Model short-circuiting explicitly

Behaviors should be able to stop the remaining pipeline intentionally. Duplicate inbox detection should short-circuit handler and saga invocation while still allowing the settlement layer to complete the message when actions are present. Future behavior such as filtering can use the same pattern.

Alternative considered: return early from nested private methods. That preserves behavior but fails to create a reusable seam for tests and future behaviors.

### Defer broader options unification

This change should not introduce a broad `MiniBusOptions` object. Existing `MiniBusProcessorOptions`, persistence options, and recoverability options are sufficient. If implementation reveals duplicated configuration access, use small internal constructor dependencies rather than public option churn.

Alternative considered: introduce `MiniBusOptions` now. It may become useful later, but this refactor is primarily architectural and should avoid public configuration churn.

## Risks / Trade-offs

- [Risk] Refactoring can accidentally alter settlement or retry behavior. -> Mitigation: preserve existing processor tests and add behavior-level ordering and short-circuit tests.
- [Risk] A mutable context can become a bag of unrelated state. -> Mitigation: keep it internal, name properties after processing concepts, and avoid exposing it to handlers.
- [Risk] Too many tiny behaviors can obscure simple control flow. -> Mitigation: split only at meaningful framework responsibilities already present in `MiniBusProcessor`.
- [Risk] Moving code from `MiniBusProcessor` may create circular dependencies between Core and Azure Functions. -> Mitigation: keep Service Bus-specific context and behaviors in the Azure Functions adapter unless proven transport-neutral.

## Migration Plan

1. Add pipeline context and behavior abstractions internally.
2. Move existing processing steps into behaviors while keeping `MiniBusProcessor` public APIs.
3. Keep existing tests green during each extraction.
4. Add behavior-level tests for ordering, isolation, failure flow, short-circuiting, and settlement decisions.
5. Remove only private helper code made obsolete by the behaviors.

Rollback is straightforward because this is an internal refactor: restore the old `MiniBusProcessor` orchestration if behavior-level extraction proves too noisy.

## Open Questions

- Should the first behavior abstraction be a simple delegate chain or a small `IMiniBusProcessingBehavior` interface?
- Should the context live under `MiniBus.AzureFunctions.Processing` initially, or should transport-neutral pieces move into `MiniBus.Core` during implementation?
