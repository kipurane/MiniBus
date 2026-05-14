## Why

`MiniBusProcessor` currently owns message adaptation, deserialization, handler invocation, saga invocation, persistence, recoverability, outbox capture, and settlement orchestration in one class. Refactoring that flow into explicit pipeline behaviors makes the framework easier to test and extend before adding observability, richer recoverability, SQL saga persistence, and developer tooling.

## What Changes

- Introduce an internal MiniBus processing pipeline with ordered behaviors and a shared processing context.
- Move current orchestration steps out of the monolithic `MiniBusProcessor` flow into explicit behaviors for message metadata, type resolution, deserialization, inbox duplicate checks, handler invocation, saga invocation, outbox capture, persistence commit, recoverability, delayed retry scheduling, and settlement decisions.
- Add a pipeline context that carries received message metadata, headers, resolved message type, deserialized payload, handler-facing `MiniBusContext`, recoverability state, outgoing operations, persistence state, saga state, and settlement decisions.
- Preserve current Azure Functions behavior for no-settlement processing, settlement-enabled processing, immediate retries, delayed retries, dead-lettering, duplicate inbox handling, SQL outbox capture, direct dispatch, and saga invocation.
- Add focused unit tests for behavior ordering, behavior isolation, short-circuit behavior, failure flow, duplicate inbox handling, outbox capture, saga invocation, and settlement decisions.
- Defer a broad public `MiniBusOptions` core configuration object unless implementation shows a concrete need; this change should use the existing `MiniBusProcessorOptions` surface.

## Capabilities

### New Capabilities

- `core-processing-pipeline`: Defines the internal processing pipeline, behavior ordering, shared pipeline context, short-circuit semantics, failure propagation, and test coverage expectations.

### Modified Capabilities

- `azure-functions-adapter`: Clarifies that the Azure Functions adapter delegates message processing to the internal pipeline while preserving existing public overloads and settlement behavior.
- `minibus-core`: Clarifies the core processing seams needed for handler-facing context, outgoing operations, and transport-independent behavior invocation without introducing transport dependencies.

## Impact

- `src/MiniBus.AzureFunctions/Processing/MiniBusProcessor.cs`: becomes a thin adapter over the internal pipeline while retaining current public APIs.
- New pipeline types, likely under `src/MiniBus.AzureFunctions/Processing` or a core processing namespace if transport-neutral enough.
- Existing core collaborators: `MessageHandlerInvoker`, `SagaInvoker`, `RecoverabilityDecisionMaker`, `IMiniBusPersistenceSessionFactory`, `MiniBusOutboxOperationCollector`, `MiniBusReceivedMessageContext`, and Azure Service Bus retry scheduler integration.
- `tests/MiniBus.AzureFunctions.Tests`: expanded unit coverage for pipeline behavior ordering, isolation, short-circuiting, recoverability, persistence, saga, and settlement flows.
- Specs for `core-processing-pipeline`, `azure-functions-adapter`, and `minibus-core`.
