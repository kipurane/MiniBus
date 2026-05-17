## 1. Metrics Contracts

- [x] 1.1 Add an internal processing metrics collaborator, such as `MiniBusProcessingMetrics`, based on `System.Diagnostics.Metrics`.
- [x] 1.2 Define stable processing Meter name/version, instrument names, units, descriptions, and tag name constants.
- [x] 1.3 Add an internal SQL outbox metrics collaborator, such as `SqlMiniBusOutboxMetrics`, with stable Meter/instrument/tag constants.
- [x] 1.4 Document in code comments that Meter names, instrument names, units, and tag names are observability contracts.
- [x] 1.5 Ensure metrics require no OpenTelemetry SDK, exporter, dashboard, or DI registration to remain safe when unused.

## 2. Processing Attempt Metrics

- [x] 2.1 Start processing attempt timing after received headers are loaded and the processing attempt begins.
- [x] 2.2 Record processing attempt duration and attempt count for completed processing.
- [x] 2.3 Record processing attempt duration, attempt count, and duplicate count for skipped duplicate inbox messages.
- [x] 2.4 Record processing attempt duration, attempt count, retry count, and retry kind for immediate retry decisions.
- [x] 2.5 Record processing attempt duration, attempt count, retry count, and retry kind for successful delayed retry scheduling.
- [x] 2.6 Record processing attempt duration, attempt count, and dead-letter count for dead-letter outcomes.
- [x] 2.7 Record processing attempt duration, attempt count, and failure count for propagated processing failures.
- [x] 2.8 Preserve existing structured logging, tracing, audit, recoverability, and settlement behavior while adding metrics.

## 3. Handler and Saga Metrics

- [x] 3.1 Extend internal handler invocation diagnostics so each handler invocation can be timed without changing public handler APIs.
- [x] 3.2 Record handler duration tagged with endpoint, message type, handler type, and bounded handler outcome.
- [x] 3.3 Ensure multiple handlers on one message produce separate handler duration measurements.
- [x] 3.4 Extend internal saga invocation diagnostics so each saga invocation can be timed without changing public saga APIs.
- [x] 3.5 Record saga handling duration tagged with endpoint, message type, saga type, and bounded saga outcome.
- [x] 3.6 Record saga completion counts when saga diagnostics report completed saga data.

## 4. SQL Outbox Dispatch Metrics

- [x] 4.1 Instrument `SqlMiniBusOutboxDispatcher.DispatchPendingAsync` with batch duration and batch count metrics.
- [x] 4.2 Record claimed, dispatched, and failed operation counts for each SQL outbox dispatch batch.
- [x] 4.3 Record operation duration and operation count metrics for successful outbox operation dispatches.
- [x] 4.4 Record operation duration and operation count metrics for failed outbox operation dispatches that remain retryable.
- [x] 4.5 Ensure SQL outbox metrics do not change existing claim, mark-dispatched, mark-failed, cancellation, or retry semantics.

## 5. Cardinality and No-Listener Behavior

- [x] 5.1 Keep metric tags limited to bounded values such as endpoint, message type, outcome, handler type, saga type, retry kind, and outbox operation kind.
- [x] 5.2 Exclude message id, correlation id, causation id, saga correlation id, SQL row id, outgoing transport message id, exception message, dead-letter description, and message body values from metric tags.
- [x] 5.3 Guard expensive timing and tag construction with instrument listener checks where practical.
- [x] 5.4 Add no-listener verification showing processing and outbox dispatch still work without metric configuration.

## 6. Tests

- [x] 6.1 Add `MeterListener`-based test helpers that capture MiniBus metrics without OpenTelemetry SDK packages.
- [x] 6.2 Add tests for processing Meter name, instrument names, units, tags, and successful processing measurements.
- [x] 6.3 Add tests for immediate retry, delayed retry, dead-letter, duplicate skip, and propagated failure metrics.
- [x] 6.4 Add tests for handler duration metrics, including multiple handlers where practical.
- [x] 6.5 Add tests for saga duration and saga completion metrics.
- [x] 6.6 Add tests for SQL outbox dispatch batch and operation metrics for successful and failed dispatches.
- [x] 6.7 Add tests proving high-cardinality identifiers are not emitted as metric tags.

## 7. Documentation and Verification

- [x] 7.1 Document processing and SQL outbox Meter names, instrument names, units, and key tag names in the Azure Functions README or observability docs.
- [x] 7.2 Document that MiniBus emits BCL metrics and applications remain responsible for OpenTelemetry SDK, exporter, collector, dashboard, and alert configuration.
- [x] 7.3 Update `openspec/project.md` to mark the Observability metrics checklist item complete.
- [x] 7.4 Run the relevant Azure Functions and SQL persistence test suites.
- [x] 7.5 Run OpenSpec validation for `add-processing-metrics`.
