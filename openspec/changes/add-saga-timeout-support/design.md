## Context

MiniBus already has the primitives needed to deliver timeout messages:

- `MiniBusContext.Schedule(...)` captures or directly dispatches scheduled messages.
- SQL outbox persistence stores `Schedule` operations with due times and dispatches them after successful processing.
- Azure Service Bus transport can schedule persisted or direct outgoing messages.
- Saga invocation already loads existing state, skips completed state, saves successful changes, and flows persistence/concurrency failures through normal processing.

The missing piece is the saga-facing contract. Today a saga can call `MiniBusContext.Schedule(...)` manually, but there is no explicit timeout concept, no discoverable timeout API, and no documented rule that timeout messages continue existing saga state by correlation.

## Goals / Non-Goals

**Goals:**

- Provide a clear saga timeout API that schedules timeout messages through the existing MiniBus scheduling path.
- Treat timeout messages as normal MiniBus messages so existing serialization, routing, headers, recoverability, and saga invocation behavior apply.
- Make timeout correlation explicit and continuing by default: timeout messages should normally load existing saga state and not start new sagas.
- Ensure SQL outbox-enabled saga processing stores timeout schedules atomically with saga state changes.
- Preserve direct-dispatch behavior for applications that run without SQL outbox.
- Keep the design open for future SQL-managed timeout storage without changing saga handler code.

**Non-Goals:**

- Do not add a SQL timeout table, SQL timeout dispatcher, cancellation table, or background polling mechanism in this change.
- Do not implement timeout cancellation or replacement semantics beyond what applications can model in saga state.
- Do not introduce Azure Service Bus SDK types into saga handlers or saga data.
- Do not change existing `MiniBusContext.Schedule(...)` behavior for non-saga handlers.

## Decisions

### Add a saga timeout marker contract

Introduce a transport-independent marker such as `ISagaTimeout : IMessage` in the core saga contracts. Timeout message types remain ordinary MiniBus messages for serialization, routing, dispatch, and receive-side processing; the marker makes intent discoverable and lets saga timeout APIs constrain accepted message types.

Alternative considered: use any `IMessage` as a timeout. That keeps the surface smaller, but it makes timeout messages indistinguishable from arbitrary scheduled messages and weakens documentation, analyzers, and future SQL-managed timeout support.

### Add saga-facing timeout request helpers

Add protected saga helper methods such as:

- `RequestTimeout<TTimeout>(TTimeout timeout, DateTimeOffset dueTime, MiniBusContext context, CancellationToken cancellationToken = default)`
- `RequestTimeout<TTimeout>(TTimeout timeout, TimeSpan delay, MiniBusContext context, CancellationToken cancellationToken = default)`

The helper delegates to `context.Schedule(...)`; it does not introduce a separate runtime scheduler. Sagas still receive `MiniBusContext` in their handler method, so the helper can stay explicit about the scheduling context without making saga instances hold per-invocation context state.

Alternative considered: attach `MiniBusContext` to the saga before invocation and expose a context-free `RequestTimeout(...)`. That reads nicely but adds hidden mutable invocation state to saga instances, which is awkward when sagas are resolved from dependency injection.

### Timeout messages continue sagas through existing correlation rules

Timeout messages are handled with `IHandleSagaMessages<TTimeout>` and mapped with existing saga correlation APIs. The default documented pattern is to call `Correlate<TTimeout>(...)`, not `StartsWith<TTimeout>(...)`, so a timeout that arrives after saga completion or without matching saga state does not create a new saga by accident.

MiniBus will not infer saga correlation id from headers in this change. The timeout payload should carry the correlation value needed by the saga mapper, keeping correlation behavior consistent with all other saga messages.

Alternative considered: add a dedicated timeout correlation convention based on saga data correlation id headers. That could reduce payload ceremony, but it would add a second correlation path and make timeout processing less like normal saga messages.

### Use Service Bus scheduled messages as the timeout mechanism

Requested timeouts become scheduled outgoing operations. With SQL outbox enabled, they are captured in the same persistence commit as inbox state, saga state changes, and other outgoing operations. Without SQL outbox, they are scheduled directly by Azure Service Bus dispatch.

This preserves current operational behavior:

- SQL outbox gives atomic capture before dispatch.
- Azure Service Bus owns the future enqueue time.
- Received timeout messages enter MiniBus through the normal Azure Functions processing pipeline.

Alternative considered: add a SQL timeout table now. That would provide framework-owned inspection and future cancellation hooks, but it duplicates scheduling responsibility before MiniBus has proven the saga-facing API shape.

### Preserve existing outgoing metadata semantics

Timeout scheduling should use the same outgoing metadata behavior as other scheduled operations:

- The scheduled message receives MiniBus message type metadata from the transport message factory.
- Correlation id follows the current `MiniBusContext` correlation id.
- Causation id identifies the message that requested the timeout.
- SQL outbox dispatch reuses the deterministic outgoing message id for retry-safe replay.

No timeout-specific header is required for receive-side routing because message type metadata and saga correlation rules identify the timeout handler.

## Risks / Trade-offs

- Service Bus scheduled messages are the timeout source of truth → Applications cannot query or cancel timeout requests through MiniBus SQL state in this change.
- Duplicate timeout requests can enqueue multiple future messages → Saga handlers should model idempotence with saga state, and tests should document that completed sagas ignore late timeout messages.
- Timeout payloads must include a correlation value → Documentation and samples need to show the pattern clearly.
- Direct dispatch has no atomic transaction with saga state → This matches existing direct `Send`, `Publish`, and `Schedule` behavior; durable workflows should enable SQL outbox.

## Migration Plan

This is additive. Existing sagas continue to compile and can keep calling `MiniBusContext.Schedule(...)` directly. New timeout APIs provide a clearer saga-specific path for new or updated workflows.

No runtime SQL migration is required because this change does not add a SQL timeout table. Applications using SQL outbox already need the existing inbox/outbox schema.

## Open Questions

- Should the timeout marker be named `ISagaTimeout`, `ISagaTimeoutMessage`, or `ITimeoutMessage`?
- Should the helper live on `MiniBusSaga<TData>` only, or should MiniBus also expose a `MiniBusContext` extension for non-inheriting helper usage?
