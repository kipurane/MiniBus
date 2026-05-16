## Why

MiniBus has production processing, recoverability, inbox/outbox, sagas, claim-check payloads, and audit support, but framework-level processing is still mostly opaque unless application handlers log their own details. Structured processing logs are the smallest useful observability slice because they make the existing pipeline explainable through `Microsoft.Extensions.Logging` while establishing stable diagnostic metadata that later tracing and metrics can reuse.

## What Changes

- Add provider-neutral structured logging integration to the MiniBus processing pipeline using `ILogger` or `ILoggerFactory` from dependency injection.
- Create a correlation-aware processing log scope for each received message so MiniBus processing logs can consistently carry endpoint, message, correlation, retry, handler, saga, and outcome metadata when known.
- Emit structured lifecycle and outcome logs for message processing start, successful completion, retry, delayed retry scheduling, dead-lettering, duplicate inbox skips, saga completion, and outbox dispatch outcomes where the current pipeline has enough context.
- Define stable diagnostic property names for endpoint name, message type, message id, correlation id, causation id, retry attempt, delayed retry attempt, handler type, saga type, and saga correlation id.
- Add focused verification hooks or in-memory logger tests for event names, log levels, scopes, structured property keys, and outcome metadata.
- Preserve existing public handler APIs, Azure Functions processing overloads, transport contracts, and application logging provider choices.
- Keep OpenTelemetry activities, metrics, dashboards, manual retry tooling, and external logging sink packages out of scope for this first observability feature.

## Capabilities

### New Capabilities

- `structured-processing-logging`: Defines MiniBus framework-level structured processing logs, correlation-aware log scopes, stable diagnostic property names, lifecycle/outcome events, and verification expectations.

### Modified Capabilities

None.

## Impact

- `src/MiniBus.AzureFunctions/Processing/Pipeline`: gains a logging behavior or focused diagnostics collaborator that observes processing without expanding `MiniBusProcessor`.
- `src/MiniBus.AzureFunctions/DependencyInjection`: may register logging diagnostics services while relying on the host application's existing `Microsoft.Extensions.Logging` setup.
- `src/MiniBus.AzureFunctions/Processing/Pipeline/MiniBusProcessingContext.cs`: may gain internal diagnostic fields or helpers for outcome, handler, saga, retry, and outbox metadata already available during processing.
- `tests/MiniBus.AzureFunctions.Tests` and possibly `tests/MiniBus.AcceptanceTests`: gain in-memory logger or test sink coverage for scope properties, event metadata, and representative processing outcomes.
- Public application handler APIs, Azure Functions trigger-facing APIs, persistence APIs, transport contracts, and logging providers remain unchanged.
