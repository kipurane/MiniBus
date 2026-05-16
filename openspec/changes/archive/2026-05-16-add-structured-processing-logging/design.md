## Context

MiniBus processing now runs through an internal Azure Functions pipeline. The current pipeline maps received headers, opens persistence, checks the inbox, resolves and deserializes message types, creates the handler-facing context, invokes handlers, invokes sagas, commits persistence/outbox work, and then lets `MiniBusProcessor` apply recoverability, audit writing, and settlement at the terminal outcome boundary.

That shape is a good fit for structured logging because most diagnostic facts already exist in `MiniBusProcessingContext`, but not all at the same point in time. Some facts are known before the pipeline runs, such as endpoint name and Service Bus message id. Others become known later, such as resolved message type, handler context, outbox operations, saga activity, recoverability decision, settlement decision, and duplicate short-circuiting.

This change should add framework-level logs without requiring application handlers to change their logging, without introducing OpenTelemetry APIs yet, and without choosing a logging sink for applications.

## Goals / Non-Goals

**Goals:**

- Emit structured MiniBus processing logs through `Microsoft.Extensions.Logging`.
- Put every MiniBus processing log for a received message inside a correlation-aware scope when correlation metadata is available.
- Define stable diagnostic property names that can be reused by future OpenTelemetry activity tags and metrics dimensions.
- Log lifecycle and terminal outcomes for normal processing, duplicate inbox skips, retry decisions, delayed retry scheduling, dead-lettering, saga completion, and outbox dispatch where the current pipeline exposes enough context.
- Keep handler-facing APIs, Azure Functions processing overloads, transport message contracts, and logging provider choices unchanged.
- Add focused tests that inspect structured state rather than relying on formatted log text.

**Non-Goals:**

- OpenTelemetry `ActivitySource`, traces, spans, or baggage.
- Metrics or `Meter` instrumentation.
- Dashboards, manual retry tooling, or log querying.
- New logging sink/provider dependencies.
- Broad public observability configuration.
- Retrofitting every lower-level transport or persistence class with logging in this first slice.

## Decisions

### Use provider-neutral ILogger diagnostics

MiniBus will use `ILogger`/`ILoggerFactory` from dependency injection and emit structured state through normal logging APIs. Applications remain responsible for registering logging providers and choosing sinks.

Alternative considered: add a MiniBus-specific logging abstraction. That would add indirection without providing value because `Microsoft.Extensions.Logging` already gives provider-neutral structured state, scopes, levels, event ids, and testable behavior.

### Add a focused diagnostics collaborator instead of growing MiniBusProcessor

Add an internal diagnostics service, for example `MiniBusProcessingLogger`, that owns event ids, event names, property names, scope construction, and outcome logging. `MiniBusProcessor` and pipeline behaviors should call this collaborator at lifecycle boundaries rather than embedding logging templates throughout orchestration code.

Alternative considered: add logging directly to each behavior and processor branch. That is expedient but scatters event naming and property construction, making it easier for logs and future telemetry names to drift.

### Start the processing scope after header mapping

For settlement-enabled processing, `MiniBusProcessor` already invokes `ReceivedMessageHeadersBehavior` before recoverability loops so retry decisions can update headers. Logging should begin after headers are available and should recreate or refresh scope metadata for immediate retry attempts when headers change.

For no-settlement processing, the pipeline can use an early logging behavior immediately after received headers are mapped. The scope should include values known at the start and later logs should include additional structured properties as they become known.

Alternative considered: start a scope before header mapping. That would miss normalized MiniBus headers and would force each log to duplicate correlation fallback logic.

### Use stable property names across scopes and log entries

Diagnostic state should use stable PascalCase property keys aligned with existing MiniBus concepts:

- `EndpointName`
- `MessageType`
- `MessageId`
- `CorrelationId`
- `CausationId`
- `RetryAttempt`
- `DelayedRetryAttempt`
- `HandlerType`
- `SagaType`
- `SagaCorrelationId`
- `ProcessingOutcome`
- `OutboxOperationCount`
- `DeadLetterReason`

Future OpenTelemetry work can map these to semantic tags such as `messaging.destination.name` and `minibus.correlation_id` without changing log contracts.

Alternative considered: use OpenTelemetry semantic names directly in log properties. That would be useful later, but it makes logs less idiomatic for .NET structured logging and prematurely commits this feature to trace naming choices.

### Treat outcome logs as terminal boundary events

The diagnostics collaborator should emit terminal outcome logs from the same places that currently invoke audit writing and settlement:

- completed
- skipped duplicate
- immediate retry selected
- delayed retry scheduled
- dead-lettered
- propagated failure when MiniBus does not settle

This avoids double logging terminal outcomes and keeps outcome logs aligned with the actual processing result. Immediate retry should be logged as an attempt outcome, then the next attempt can log a new processing start.

Alternative considered: have each behavior log success/failure independently. That can be useful for deep tracing later, but it risks noisy logs and ambiguous outcomes in this first slice.

### Capture handler, saga, and outbox diagnostics at narrow source points

Handler invocation behavior is the best place to report handler type when known because `MessageHandlerInvoker` owns handler discovery/invocation. If the current invoker does not expose individual handler types, this change can add a small internal diagnostic hook or best-effort aggregate metadata without changing public handler APIs.

Saga diagnostics should be emitted when `SagaInvocationBehavior` or `SagaInvoker` knows a saga type and correlation id. If the existing saga invoker cannot expose that metadata cleanly, this change should add an internal callback/result model instead of parsing logs or using reflection in tests.

Outbox diagnostics should be emitted after successful persistence commit or dispatch boundaries where `OutboxOperations` are known. The first slice can log operation counts and outcome, then defer per-operation transport telemetry to future tracing/metrics work.

Alternative considered: infer handler, saga, and outbox details only from final context. That keeps fewer hooks but loses details that are only visible during invocation.

### Keep log levels conservative

Use `Information` for processing start and terminal success/outcome logs, `Warning` for retry, delayed retry, duplicate skip, and dead-letter outcomes, and `Error` for propagated failures or failures that prevent MiniBus from settling successfully. Tests should assert event ids/names and structured properties more strongly than message text.

Alternative considered: log successful processing at `Debug`. That keeps default logs quiet, but the purpose of this first observability slice is operational explainability without requiring users to lower log levels.

## Risks / Trade-offs

- Structured property names become a public-ish contract -> Define them centrally and cover them with tests.
- Logging around immediate retry can accidentally duplicate or blur outcomes -> Log attempt outcomes explicitly and create a fresh start log for each retry attempt.
- Handler and saga metadata may require small internal API changes -> Keep changes internal and avoid changing handler-facing contracts.
- Extra logging can add allocation overhead -> Use `LoggerMessage` patterns or centralized templates where practical, and avoid expensive metadata construction when the relevant log level is disabled.
- Scope values can be incomplete early in processing -> Scope carries stable known values, while later log entries include additional per-event properties as they become available.

## Migration Plan

No application migration is required. Applications that already register logging providers will start seeing MiniBus framework logs. Applications without logging providers continue to process messages normally. Rolling back means removing the diagnostics registrations/calls or raising logging filters for the MiniBus categories.

## Open Questions

- Should successful processing logs be `Information` by default for the first release, or should the implementation offer an internal constant that can be changed before OpenTelemetry and metrics land?
- Should handler diagnostics record one log per handler invocation or one aggregate log per message with handler type names when multiple handlers exist?
