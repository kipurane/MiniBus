## Context

MiniBus processing now has structured logging through an internal processing logger. That logger defines stable event ids, outcome names, property names, and lifecycle boundaries for processing attempts, handler invocation, saga invocation/completion, recoverability outcomes, duplicate skips, and outbox commits. Those same boundaries are the natural places to emit trace activities and events.

The next observability need is OpenTelemetry-friendly tracing. MiniBus should use BCL `System.Diagnostics.ActivitySource` directly so applications can opt into trace export with OpenTelemetry or any other Activity listener without MiniBus referencing the OpenTelemetry SDK or exporter packages.

## Goals / Non-Goals

**Goals:**

- Add provider-neutral ActivitySource tracing for MiniBus message processing.
- Emit one root processing activity per received message processing attempt.
- Define stable ActivitySource name, activity names, and MiniBus tag names as observability contracts.
- Reuse the structured logging diagnostic vocabulary where possible while mapping to common messaging semantic tags where appropriate.
- Record outcome, retry, duplicate, dead-letter, handler, saga, outbox, and failure metadata on activities or activity events.
- Keep tracing no-op and low overhead when no listener is attached.
- Preserve existing handler APIs, processing overloads, structured logging behavior, transport contracts, and application logging provider choices.

**Non-Goals:**

- Metrics or `Meter` instrumentation.
- OpenTelemetry SDK, exporters, resource configuration, collector setup, dashboards, or sampling policy.
- Full distributed trace propagation across outgoing Service Bus messages.
- Public tracing configuration beyond stable source/activity/tag names.
- Replacing or modifying structured logging contracts.
- Manual retry tooling or dashboards.

## Decisions

### Use System.Diagnostics.ActivitySource directly

Add an internal tracing collaborator, for example `MiniBusProcessingTracer`, that owns the ActivitySource name, activity names, tag names, activity creation, status mapping, and event creation. This keeps tracing provider-neutral and lets applications decide whether to install OpenTelemetry and which exporters or sampling rules to use.

Alternative considered: reference OpenTelemetry packages directly and expose OpenTelemetry-specific registration. That would be convenient for one hosting path, but it would turn MiniBus into an exporter/configuration participant instead of a framework that emits standard .NET diagnostics.

### Treat ActivitySource identifiers as stable contracts

Use a stable source name such as `MiniBus` or `MiniBus.Processing` and a stable activity name `MiniBus.Process` for the root processing attempt. The source should include an assembly/package version when practical. The chosen name must be documented because applications will use it in trace filters such as `AddSource(...)`.

Alternative considered: use the logger category `MiniBus.Processing` as the ActivitySource name automatically. That improves symmetry, but source names and logger categories do not have to evolve together; the design should choose and document the source name deliberately.

### Create one root activity per processing attempt

Start the processing activity after received message headers are loaded, matching the structured logging scope. Immediate retry attempts should get their own root processing activity because they are separate processing attempts with updated retry metadata. The activity should end after the terminal attempt outcome is known.

Alternative considered: create one activity for the entire `ProcessAsync` call including immediate retries. That hides per-attempt retry metadata and makes outcomes harder to reason about.

### Use tags for stable message and outcome metadata

Populate tags from the current processing context:

- `messaging.system = azure_service_bus`
- `messaging.destination.name` when destination can be determined
- `minibus.endpoint`
- `minibus.message_type`
- `minibus.message_id`
- `minibus.correlation_id`
- `minibus.causation_id`
- `minibus.retry_attempt`
- `minibus.delayed_retry_attempt`
- `minibus.handler_type`
- `minibus.saga_type`
- `minibus.saga_correlation_id`
- `minibus.processing_outcome`
- `minibus.outbox_operation_count`
- `minibus.dead_letter_reason`

Use MiniBus tag names for MiniBus-specific concepts and common messaging semantic tags for transport concepts. Structured logging property names remain unchanged; tracing tags map from the same concepts into lower-case semantic tag names.

Alternative considered: use the exact PascalCase structured logging property names as activity tags. That would reduce mapping code, but trace tags are more useful when they follow common OpenTelemetry naming conventions.

### Prefer activity events before child activities in the first slice

Record activity events for low-risk milestones such as handler invocation, saga invocation, saga completion, immediate retry decision, delayed retry scheduling, dead-letter decision, duplicate skip, and outbox commit. Child activities should be added only if implementation shows they can be created without confusing span hierarchy or adding significant overhead.

Alternative considered: create child activities for every handler, saga, and persistence operation. That may eventually be valuable, but the first tracing slice should establish stable processing traces without over-modeling internals.

### Map terminal outcomes to ActivityStatusCode

Successful processing, duplicate skip, immediate retry decision, and successful delayed retry scheduling should keep activity status successful or unset. Propagated failures, delayed retry scheduling failures, persistence failures, and audit failures should set error status and record exception details. Dead-letter outcomes should set error status because the message reached a failed terminal settlement even though MiniBus handled the settlement path.

Alternative considered: leave dead-letter activity status successful because settlement succeeded. That would be misleading operationally because the business processing outcome failed.

### Keep tracing no-op when no listener is attached

Use `ActivitySource.StartActivity(...)` and guard richer tag/event work where practical so MiniBus avoids expensive allocations when no listener samples the activity. The implementation should not require DI registration to disable tracing.

Alternative considered: add public options to enable/disable tracing. ActivitySource already provides listener-based no-op behavior, so a public switch would add configuration without clear need.

## Risks / Trade-offs

- ActivitySource and tag names become observability contracts -> document names and cover them with listener-based tests.
- Semantic conventions can evolve -> use stable current messaging tags sparingly and keep MiniBus-specific tags under `minibus.*`.
- Destination name may not always be available from the receive context -> tag it only when MiniBus can determine it without guessing.
- Too many child spans can make traces noisy -> start with root activity plus events; add child activities only where clearly justified.
- Error status semantics can be debatable for dead-letter/delayed retry -> document chosen mapping and test representative outcomes.
- Trace context propagation to outgoing messages can expand scope quickly -> defer full propagation unless a minimal receive-side hook is clearly needed for the root processing activity.

## Migration Plan

No application migration is required. Applications that already configure OpenTelemetry can add the MiniBus ActivitySource name to their trace builder to export MiniBus processing traces. Applications without Activity listeners continue processing normally with near-zero tracing overhead.

## Open Questions

- Should the ActivitySource name be `MiniBus` to represent the whole framework, or `MiniBus.Processing` to mirror the current logger category and processing-only scope?
- Should handler and saga milestones be activity events only in this slice, or should handler invocation become child activities from the start?
- Can Azure Service Bus receive destination be reliably determined from current `ServiceBusReceivedMessage` or adapter context, or should destination tags wait for a later adapter metadata change?
