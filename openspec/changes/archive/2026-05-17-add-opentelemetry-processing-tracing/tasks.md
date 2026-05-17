## 1. Tracing Foundation

- [x] 1.1 Add an internal tracing collaborator, such as `MiniBusProcessingTracer`, based on `System.Diagnostics.ActivitySource`.
- [x] 1.2 Define stable ActivitySource name, optional version, root activity name, activity event names, and MiniBus tag name constants.
- [x] 1.3 Document in code comments that ActivitySource name, activity names, and MiniBus tag names are observability contracts.
- [x] 1.4 Ensure tracing requires no OpenTelemetry SDK, exporter, or DI registration and remains no-op when no listener is attached.

## 2. Processing Activity Lifecycle

- [x] 2.1 Start a root `MiniBus.Process` activity after received message headers are loaded for settlement and no-settlement processing paths.
- [x] 2.2 Create a separate processing activity for each immediate retry attempt with updated retry metadata.
- [x] 2.3 Stop the processing activity only after terminal outcome tags, status, and events are recorded.
- [x] 2.4 Preserve existing structured logging, audit, recoverability, and settlement behavior while adding tracing.

## 3. Tags, Outcomes, and Events

- [x] 3.1 Add Azure messaging tags such as `messaging.system = azure_service_bus` and destination tags only when destination is reliably known.
- [x] 3.2 Add MiniBus tags for endpoint, message type, message id, correlation id, causation id, retry attempt, delayed retry attempt, handler type, saga type, saga correlation id, processing outcome, outbox operation count, and dead-letter reason when available.
- [x] 3.3 Record completed, skipped duplicate, immediate retry, delayed retry scheduled, dead-lettered, and failed processing outcomes on the current activity.
- [x] 3.4 Set error status and exception details for propagated failures, dead-letter outcomes, delayed retry scheduling failures, persistence failures, and audit failures.
- [x] 3.5 Add activity events or child activities for handler invocation, saga invocation, saga completion, recoverability decisions, duplicate skips, dead-letter decisions, and outbox commits where useful and low-overhead.

## 4. Tests

- [x] 4.1 Add ActivityListener-based test helpers that capture MiniBus activities without OpenTelemetry SDK packages.
- [x] 4.2 Add tests for ActivitySource name, root activity name, start/stop behavior, and no-listener behavior.
- [x] 4.3 Add tests for core messaging and MiniBus tags on successful processing.
- [x] 4.4 Add tests for immediate retry, delayed retry, dead-letter, duplicate skip, and propagated failure outcomes and status mapping.
- [x] 4.5 Add tests for handler, saga, and outbox tracing events or child activities.
- [x] 4.6 Add tests for persistence, audit, or delayed retry scheduling failures where the current test suite can exercise those failures.
- [x] 4.7 Run the focused Azure Functions tests and any affected acceptance tests.

## 5. Documentation and Backlog

- [x] 5.1 Document ActivitySource name, root activity name, key tag names, provider-neutral behavior, and OpenTelemetry SDK/exporter non-goals in the Azure Functions README.
- [x] 5.2 Update `openspec/project.md` observability checklist only for tracing items completed by this change.
- [x] 5.3 Validate the OpenSpec change status before implementation begins.
