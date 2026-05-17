## Why

MiniBus now emits structured processing logs with stable diagnostic metadata, but operators still cannot see message processing as trace spans in OpenTelemetry-enabled hosts. Adding provider-neutral `ActivitySource` instrumentation is the next observability slice because it lets applications export MiniBus processing traces without MiniBus taking a dependency on the OpenTelemetry SDK, exporters, dashboards, or sampling policy.

## What Changes

- Add internal `System.Diagnostics.ActivitySource`-based tracing for MiniBus message processing.
- Create a root processing activity for each received message processing attempt, named `MiniBus.Process`.
- Define stable ActivitySource name/version semantics and document the source name, activity names, and MiniBus tag names as observability contracts.
- Populate activity tags using the diagnostic vocabulary established by structured processing logging where possible, plus Azure messaging semantic tags such as `messaging.system = azure_service_bus`.
- Add MiniBus-specific tags for endpoint name, message type, message id, correlation id, causation id, retry attempt, delayed retry attempt, handler type, saga type, saga correlation id, processing outcome, outbox operation count, and dead-letter reason when available.
- Record activity status and error details for propagated failures, dead-letter outcomes, delayed retry scheduling failures, persistence failures, and audit failures where appropriate.
- Add activity events or child activities only for clear processing milestones such as handler invocation, saga invocation/completion, retry decision, dead-letter decision, duplicate skip, and outbox commit.
- Ensure tracing is no-op and low overhead when no listener is attached.
- Add focused tests with `ActivityListener` or equivalent hooks to verify source name, activity name, tags, status, events or children, and representative outcomes.
- Keep metrics, OpenTelemetry SDK/exporter configuration, dashboards, distributed outgoing trace propagation, and structured logging contract changes out of scope.

## Capabilities

### New Capabilities

- `opentelemetry-processing-tracing`: Defines ActivitySource-based MiniBus processing traces, stable activity/tag names, outcome/error tagging, listener/no-op behavior, and tracing verification expectations.

### Modified Capabilities

None.

## Impact

- `src/MiniBus.AzureFunctions/Processing/Pipeline`: gains an internal tracing collaborator or processing diagnostics layer alongside structured logging.
- `src/MiniBus.AzureFunctions/Processing/MiniBusProcessor.cs`: starts and completes processing activities at the same attempt/outcome boundaries used for structured logs and audit writing.
- Handler, saga, persistence, recoverability, and outbox processing boundaries may call a tracing collaborator to add events, child activities, or tags where metadata is only known at that point.
- `tests/MiniBus.AzureFunctions.Tests`: gains ActivityListener-based tests for processing start/completion, failures, retries, duplicate skips, handler diagnostics, saga diagnostics, and outbox diagnostics.
- `src/MiniBus.AzureFunctions/README.md` and `openspec/project.md`: gain brief tracing documentation and backlog updates.
- No public handler APIs, Azure Functions processing overloads, logging contracts, transport contracts, OpenTelemetry SDK dependencies, exporters, or metrics behavior are introduced by this change.
