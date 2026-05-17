## Context

MiniBus processing now has two provider-neutral observability layers:

- `MiniBusProcessingLogger` emits structured processing logs with stable event ids, property names, scope values, and outcome names.
- `MiniBusProcessingTracer` emits `ActivitySource`-based processing traces with stable source, activity, event, and tag names.

The remaining Observability gap is aggregate metrics. Operators need counters and duration histograms for processing attempts, handler invocation, recoverability decisions, saga handling, and SQL outbox dispatch without changing handler code or requiring MiniBus to own OpenTelemetry SDK/exporter configuration.

The current processing pipeline already has the right lifecycle boundaries:

- `MiniBusProcessor` owns processing attempt start/stop and terminal outcomes.
- `HandlerInvocationBehavior` owns handler invocation.
- `SagaInvocationBehavior` receives saga diagnostics from `SagaInvoker`.
- `PersistenceBehavior` observes outbox capture/commit during processing.
- `SqlMiniBusOutboxDispatcher` owns SQL outbox replay batch and operation dispatch.

Metrics should reuse those boundaries as a sibling to logging and tracing.

## Goals / Non-Goals

**Goals:**

- Add provider-neutral `System.Diagnostics.Metrics` instrumentation for MiniBus processing and SQL outbox dispatch.
- Define stable Meter names, instrument names, units, descriptions, and tag names as observability contracts.
- Record processing attempt duration and counts with terminal outcome metadata.
- Record handler invocation duration with handler/message metadata.
- Record retry, delayed retry, dead-letter, duplicate skip, failure, and completed processing counts.
- Record saga invocation duration and saga completion counts when saga metadata is available.
- Record SQL outbox dispatch batch duration, claimed/dispatched/failed counts, and operation duration/counts.
- Avoid high-cardinality tags such as message id, correlation id, causation id, saga correlation id, exception message, and dead-letter description.
- Keep metrics no-op and low overhead when no listener is attached.
- Preserve existing public APIs, processing behavior, logging contracts, tracing contracts, and application observability configuration choices.

**Non-Goals:**

- OpenTelemetry SDK dependencies, exporters, collectors, dashboards, resource configuration, or sampling policy.
- Application-specific business metrics.
- Manual retry tooling or operational dashboards.
- Changing existing structured log property names, trace source names, trace activity names, or trace tag names.
- Distributed trace propagation or metric exemplar support.

## Decisions

### Use System.Diagnostics.Metrics directly

Add internal metrics collaborators, for example `MiniBusProcessingMetrics` and `SqlMiniBusOutboxMetrics`, that own Meter creation, instrument names, tag names, tag construction, and duration recording. This mirrors `MiniBusProcessingTracer`: MiniBus emits standard .NET diagnostics and applications decide whether to export them with OpenTelemetry or any other listener.

Alternative considered: reference OpenTelemetry packages directly and expose `AddMiniBusOpenTelemetry(...)`. That would simplify one host setup path but would make MiniBus participate in SDK/exporter configuration, which the existing tracing design deliberately avoids.

### Treat Meter and instrument names as stable contracts

Use stable Meter names aligned with current observability scope:

- `MiniBus.Processing` for Azure Functions processing pipeline metrics.
- `MiniBus.Persistence.Sql` for SQL outbox dispatcher metrics.

Each Meter should include the owning assembly/package version when practical. Instrument names and tag names should be documented and tested because applications may filter, chart, and alert on them.

Representative instruments:

- `minibus.processing.attempts` counter, unit `{attempt}`
- `minibus.processing.duration` histogram, unit `s`
- `minibus.processing.retries` counter, unit `{retry}`
- `minibus.processing.dead_letters` counter, unit `{message}`
- `minibus.processing.duplicates` counter, unit `{message}`
- `minibus.processing.failures` counter, unit `{failure}`
- `minibus.handler.duration` histogram, unit `s`
- `minibus.saga.duration` histogram, unit `s`
- `minibus.saga.completions` counter, unit `{saga}`
- `minibus.sql_outbox.dispatch.batches` counter, unit `{batch}`
- `minibus.sql_outbox.dispatch.batch_duration` histogram, unit `s`
- `minibus.sql_outbox.dispatch.operations` counter, unit `{operation}`
- `minibus.sql_outbox.dispatch.operation_duration` histogram, unit `s`

Alternative considered: a single `minibus.processing.events` counter tagged with event type. A single instrument reduces surface area, but dedicated counters make common alert queries clearer and avoid forcing every consumer to know the complete event taxonomy.

### Use bounded tags only

Metric tags should prioritize aggregation. Allow bounded or code-defined dimensions:

- `minibus.endpoint`
- `minibus.message_type`
- `minibus.processing_outcome`
- `minibus.handler_type`
- `minibus.saga_type`
- `minibus.retry_kind`
- `minibus.outbox_operation_kind`
- `minibus.sql_outbox.dispatch_outcome`

Do not use high-cardinality or sensitive values as metric tags:

- message id
- correlation id
- causation id
- saga correlation id
- exception message
- dead-letter description
- outgoing transport message id
- SQL row id

Exception type is useful but can grow in unexpected ways for application exceptions. The first metrics slice should omit it from metric tags and leave that detail to logs/traces.

Alternative considered: tag all metrics with the same rich metadata used by logs and traces. That would make metrics easier to correlate with a single message, but it would create high-cardinality metric streams that are expensive and noisy in production telemetry backends.

### Measure processing attempts at the same boundary as traces

Start processing metric timing in `BeginProcessingAttempt` after received headers are loaded, matching log scope and root activity start. Stop timing when a terminal outcome is recorded: completed, skipped duplicate, immediate retry, delayed retry scheduled, dead-lettered, or failed.

Immediate retry attempts should produce separate processing duration measurements because the current processor creates separate root activities and updated retry metadata for each attempt.

Alternative considered: measure one duration for the whole `ProcessAsync` call including immediate retries. That hides attempt-level retry behavior and would diverge from the tracing model.

### Record handler duration around actual handler invocation

Measure each handler invocation around the reflected `IHandleMessages<T>.Handle(...)` task. The existing `MessageHandlerInvoker` already notifies the pipeline when a handler type is known, but duration requires timing around the actual task, so the invoker should support a diagnostic callback shape that can wrap or observe invocation completion without changing public handler APIs.

The handler duration should tag endpoint, message type, handler type, and a bounded invocation outcome such as `completed` or `failed`.

Alternative considered: measure the whole `HandlerInvocationBehavior` duration. That is simpler, but a single message can have multiple handlers, so behavior-level timing would hide per-handler latency.

### Record saga duration around each saga invocation

Measure each saga invocation where `SagaInvoker` loads/creates data, calls `Handle`, persists state, and determines completion for a specific saga. Tag endpoint, message type, saga type, and bounded saga outcome such as `handled`, `completed`, or `failed`. Count saga completions separately when `diagnostic.Completed` is true.

Alternative considered: measure the whole `SagaInvocationBehavior` duration. That is easier but loses per-saga attribution when multiple saga mappings match one message.

### Record outbox dispatch metrics in the SQL dispatcher

Record batch-level SQL outbox dispatch metrics in `SqlMiniBusOutboxDispatcher.DispatchPendingAsync`:

- batch duration
- batch count
- claimed operation count
- dispatched operation count
- failed operation count

Record operation-level duration/count around each `_dispatcher.DispatchAsync(...)` plus store status update. Tag operation kind and dispatch outcome. If `MarkFailedAsync` itself fails, record a failed operation outcome before rethrowing or surfacing the failure according to existing behavior.

Processing-time outbox capture/commit metrics should remain limited to operation count and commit timing if practical. The required "outbox dispatch duration" specifically belongs to SQL outbox replay dispatch.

Alternative considered: instrument only the transport dispatcher. That would miss SQL outbox claim/mark behavior and would not describe the operational loop that users actually run for outbox replay.

### Keep metrics low overhead when no listener is attached

Use shared process-lifetime Meters and instruments. Guard expensive timing/tag construction with instrument `Enabled` checks where practical, especially for histograms that require `Stopwatch` timestamps and tag arrays. Counter increments should still avoid building tags when no relevant instrument is enabled.

No public enable/disable option is needed because `System.Diagnostics.Metrics` already provides listener-driven activation.

Alternative considered: add MiniBus options to enable metrics. That would create a second observability switch and make configuration more complex without improving the no-listener case.

## Risks / Trade-offs

- Metric name/tag choices become long-lived contracts -> document them and cover names/tags with `MeterListener` tests.
- Too many dimensions can cause high-cardinality telemetry -> keep tags bounded and explicitly exclude IDs, correlation values, exception messages, and row identifiers.
- Handler and saga duration require deeper callback changes than logging/tracing milestones -> keep callback changes internal and preserve public handler/saga APIs.
- Dead-letter, delayed retry, and failure semantics can be counted in multiple ways -> use the existing `MiniBusProcessingOutcomes` vocabulary and test representative outcomes.
- SQL outbox dispatch can fail while recording failure metadata -> metrics must not change existing error handling or retryability semantics.
- Histograms require unit choices -> use seconds (`s`) consistently for duration instruments because it maps cleanly to OpenTelemetry conventions.

## Migration Plan

No application migration is required. Applications that already configure metrics through OpenTelemetry or another `MeterListener` can add the documented MiniBus Meter names to export the new instruments. Applications without metric listeners continue processing normally with near-zero metric overhead.

Rollback is removing the metrics change; persisted data and message contracts are unaffected.

## Open Questions

- Should SQL outbox dispatcher metrics live under `MiniBus.Persistence.Sql` only, or should there also be a framework-wide outbox Meter in `MiniBus.Processing` for future non-SQL persistence providers?
- Should processing duration use a single attempts counter plus outcome tag, or keep dedicated counters for retries, dead letters, duplicates, and failures for clearer user queries?
- Should exception type remain excluded from metric tags in the first slice, or be allowed as an opt-in bounded-ish diagnostic dimension later?
