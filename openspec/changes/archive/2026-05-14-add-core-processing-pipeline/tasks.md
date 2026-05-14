## 1. Pipeline Foundation

- [x] 1.1 Add internal pipeline context type that carries received message, actions, headers, endpoint metadata, resolved type, deserialized payload, handler context, persistence state, outbox operations, recoverability state, and settlement decision.
- [x] 1.2 Add an internal pipeline behavior abstraction and delegate/runner for ordered asynchronous behavior execution.
- [x] 1.3 Add unit tests proving behavior ordering and that behaviors can read/write shared context state.
- [x] 1.4 Decide during implementation whether any context or behavior types belong in `MiniBus.Core`; keep Service Bus-specific types in `MiniBus.AzureFunctions`.

## 2. Extract Current Processing Behaviors

- [x] 2.1 Extract received Service Bus metadata and header adaptation into a behavior or pipeline setup step.
- [x] 2.2 Extract message type resolution and deserialization into explicit behaviors.
- [x] 2.3 Extract handler-facing `MiniBusReceivedMessageContext` creation into an explicit behavior.
- [x] 2.4 Extract regular handler invocation into an explicit behavior.
- [x] 2.5 Extract saga invocation into an explicit behavior that preserves disabled/enabled saga behavior and missing saga infrastructure errors.
- [x] 2.6 Keep outgoing direct dispatch behavior unchanged when SQL outbox capture is disabled.

## 3. Persistence and Outbox Behaviors

- [x] 3.1 Extract persistence session creation and inbox message construction into explicit behavior state.
- [x] 3.2 Extract duplicate inbox detection into a short-circuiting behavior.
- [x] 3.3 Extract SQL outbox collector creation and captured operation flow into explicit behavior state.
- [x] 3.4 Extract persistence commit into an explicit behavior that wraps commit failures in `MiniBusPersistenceCommitException`.
- [x] 3.5 Add tests for duplicate inbox short-circuiting, outbox capture, persistence commit ordering, and commit failure propagation.

## 4. Recoverability and Settlement Behaviors

- [x] 4.1 Refactor settlement-enabled processing so recoverability wraps the core pipeline and preserves immediate retry loops.
- [x] 4.2 Represent settlement outcomes explicitly before invoking `IMiniBusMessageActions`.
- [x] 4.3 Extract delayed retry scheduling into an explicit behavior or service invoked by the recoverability/settlement flow.
- [x] 4.4 Preserve no-settlement processing failure propagation without settlement decisions.
- [x] 4.5 Add tests for immediate retry, delayed retry scheduling, dead-letter decisions, successful completion, and persistence commit failure preventing completion.

## 5. Processor Integration

- [x] 5.1 Refactor `MiniBusProcessor` public overloads to delegate to the pipeline while keeping public method signatures unchanged.
- [x] 5.2 Preserve dependency injection registration and construction behavior for `MiniBusProcessor`.
- [x] 5.3 Preserve existing Azure Functions adapter tests and add focused pipeline tests for behavior isolation.
- [x] 5.4 Confirm no broad public `MiniBusOptions` object is needed; document the decision in code comments or change notes only if implementation creates ambiguity.

## 6. Documentation and Verification

- [x] 6.1 Update relevant README or adapter documentation if the internal pipeline changes developer-facing troubleshooting or architecture guidance.
- [x] 6.2 Run the Azure Functions adapter test suite.
- [x] 6.3 Run the full .NET test suite.
- [x] 6.4 Run OpenSpec validation for `add-core-processing-pipeline`.
