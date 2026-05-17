## Why

MiniBus now emits structured processing logs and provider-neutral processing traces, but operators still lack metric instruments for alerting, dashboards, and aggregate health views. Processing metrics are the final planned Observability slice because they make throughput, latency, recoverability, saga, and outbox behavior measurable without requiring application handler changes or OpenTelemetry SDK dependencies.

## What Changes

- Add provider-neutral `System.Diagnostics.Metrics` instrumentation for MiniBus processing.
- Define stable Meter name/version semantics plus documented instrument and tag names as observability contracts.
- Record processing attempt duration tagged with bounded processing metadata and terminal outcome.
- Record handler invocation duration using bounded handler/message metadata.
- Record retry, delayed retry, dead-letter, duplicate skip, failure, and completed processing counts.
- Record saga invocation duration and saga completion counts where saga metadata is available.
- Record SQL outbox dispatch batch and operation metrics, including duration and success/failure counts.
- Add `MeterListener`-based tests for metric instruments, tags, no-listener behavior, and representative outcomes without adding OpenTelemetry SDK packages.
- Document metric setup and contract names in README/docs and mark the Observability metrics checklist item complete.

## Capabilities

### New Capabilities

- `processing-metrics`: Provider-neutral MiniBus metrics for processing duration, handler duration, recoverability outcomes, saga handling, and SQL outbox dispatch.

### Modified Capabilities

- None.

## Impact

- `src/MiniBus.AzureFunctions/Processing` and `src/MiniBus.AzureFunctions/Processing/Pipeline`: add internal metrics instrumentation alongside the existing processing logger and tracer.
- `src/MiniBus.Persistence.Sql`: add metrics around SQL outbox dispatch batches and individual dispatch outcomes.
- `tests/MiniBus.AzureFunctions.Tests` and `tests/MiniBus.Persistence.Sql.Tests`: add focused `MeterListener` verification for processing and outbox dispatch metrics.
- `src/MiniBus.AzureFunctions/README.md` and `openspec/project.md`: document metrics contracts and update the Observability checklist.
- No public handler APIs, processing overloads, logging contracts, tracing contracts, OpenTelemetry SDK dependencies, exporters, dashboards, or collector configuration are introduced by this change.
