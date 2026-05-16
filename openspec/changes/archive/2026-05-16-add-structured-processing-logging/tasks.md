## 1. Diagnostics Foundation

- [x] 1.1 Add internal diagnostic event id, event name, outcome, and structured property name constants for MiniBus processing logs.
- [x] 1.2 Add an internal `MiniBusProcessingLogger` or equivalent collaborator that creates scopes, checks enabled log levels, and emits structured processing diagnostics.
- [x] 1.3 Register or construct the diagnostics collaborator from dependency injection using `ILoggerFactory`/`ILogger` while preserving behavior when no logging provider is configured.
- [x] 1.4 Add processing context fields or helpers needed to carry diagnostic metadata such as current attempt, handler type, saga type, saga correlation id, and outbox operation count.

## 2. Processing Scope and Lifecycle Logs

- [x] 2.1 Start a correlation-aware processing log scope after received message headers are mapped.
- [x] 2.2 Emit a processing-start log for each processing attempt, including updated retry metadata for immediate retry attempts.
- [x] 2.3 Ensure no-settlement processing and settlement-enabled processing both use the same scope and lifecycle logging behavior.
- [x] 2.4 Keep scope values stable for known metadata and add richer per-event properties as message type, handler, saga, recoverability, and outbox metadata become known.

## 3. Outcome and Invocation Diagnostics

- [x] 3.1 Emit one completed outcome log for successful non-duplicate processing.
- [x] 3.2 Emit one skipped-duplicate outcome log when inbox duplicate detection short-circuits processing.
- [x] 3.3 Emit immediate retry, delayed retry scheduled, dead-lettered, and propagated failure outcome logs from the recoverability/settlement boundaries.
- [x] 3.4 Emit handler invocation diagnostics with handler type, message metadata, and correlation metadata when handlers are invoked.
- [x] 3.5 Emit saga invocation and saga-completed diagnostics with saga type and saga correlation id when saga processing exposes those values.
- [x] 3.6 Emit outbox diagnostics only when outbox operations are captured and committed or dispatched as part of successful processing.
- [x] 3.7 Verify outcome logging does not duplicate final outcome events for a single processing attempt.

## 4. Tests

- [x] 4.1 Add an in-memory logger/test sink for capturing structured state, scopes, event ids or event names, levels, and exceptions without asserting rendered message text.
- [x] 4.2 Add tests for processing-start scopes and stable property names with and without correlation metadata.
- [x] 4.3 Add tests for successful completion, duplicate inbox skip, immediate retry, delayed retry, dead-letter, and propagated failure outcome logs.
- [x] 4.4 Add tests for handler diagnostics and best-effort or explicit handler type metadata.
- [x] 4.5 Add tests for saga invocation and saga-completed diagnostics when saga metadata is available.
- [x] 4.6 Add tests for outbox diagnostics with captured operations and absence of misleading outbox logs when no operations exist.
- [x] 4.7 Run the focused Azure Functions test suite and any affected acceptance tests.

## 5. Documentation and Backlog

- [x] 5.1 Add brief README or API notes describing MiniBus structured processing logs, scope/property names, and provider-neutral logging behavior.
- [x] 5.2 Update `openspec/project.md` observability checklist only for items completed by this change.
- [x] 5.3 Validate the OpenSpec change status before implementation begins.
