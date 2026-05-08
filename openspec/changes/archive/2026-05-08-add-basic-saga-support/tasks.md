## 1. Core saga contracts

- [x] 1.1 Add saga data identity contracts to `MiniBus.Core`, including id, correlation id, and completion state.
- [x] 1.2 Add a saga base type or marker contract for saga classes with attached saga data.
- [x] 1.3 Add saga handler contracts for handling MiniBus messages with saga state.
- [x] 1.4 Add saga lifecycle operations for marking saga state complete.
- [x] 1.5 Add core unit tests for saga contract usage and completion behavior.

## 2. Correlation mapping and finding

- [x] 2.1 Add explicit saga correlation mapping APIs for starting messages and continuing messages.
- [x] 2.2 Support simple property-to-property correlation from message data to saga data.
- [x] 2.3 Add a saga finder abstraction for custom correlation logic.
- [x] 2.4 Fail fast with clear errors for missing or ambiguous correlation mappings.
- [x] 2.5 Add unit tests for starting-message correlation, continuing-message correlation, missing mappings, and custom finder behavior.

## 3. Saga persistence abstraction

- [x] 3.1 Add a transport-independent saga persistence abstraction for loading saga data by saga type and correlation id.
- [x] 3.2 Add persistence operations for creating, saving, and completing saga data.
- [x] 3.3 Include optimistic-concurrency-ready metadata such as a version token or revision value.
- [x] 3.4 Add in-memory/fake persistence support for tests if no production persistence package exists.
- [x] 3.5 Add tests for load, create, save, complete, and completed-state behavior.

## 4. Saga invocation behavior

- [x] 4.1 Discover or register saga handlers separately from regular handlers.
- [x] 4.2 Load existing saga state before invoking a saga handler for a continuing message.
- [x] 4.3 Create saga state before invoking a saga handler for a configured starting message when no existing state is found.
- [x] 4.4 Save saga state after successful saga handler execution.
- [x] 4.5 Do not save saga state when saga handler execution fails.
- [x] 4.6 Do not invoke saga handlers for completed saga state.
- [x] 4.7 Add tests for saga start, existing saga load, failed handler behavior, and completed saga behavior.

## 5. Azure Functions integration

- [x] 5.1 Wire saga invocation behavior into Azure Functions processing through core abstractions.
- [x] 5.2 Ensure saga handlers receive only MiniBus message instances, saga data, `MiniBusContext`, and `CancellationToken`.
- [x] 5.3 Preserve MiniBus headers, correlation id, and causation id while invoking saga handlers and dispatching outgoing operations.
- [x] 5.4 Ensure saga persistence failures flow through existing recoverability behavior.
- [x] 5.5 Add Azure Functions tests for saga processing without exposing Azure Functions or Service Bus types to saga handlers.

## 6. Optional SQL integration check

- [x] 6.1 Confirmed `MiniBus.Persistence.Sql` does not exist; no SQL saga persistence added in this change.
- [x] 6.2 If SQL persistence does not exist, document SQL saga persistence as deferred and keep this change limited to the persistence abstraction.
- [x] 6.3 SQL integration was not implemented because the SQL persistence package does not exist.

## 7. Documentation and verification

- [x] 7.1 Add documentation for defining saga data, configuring correlation, handling messages, and marking a saga complete.
- [x] 7.2 Add a sample saga for a simple long-running workflow.
- [x] 7.3 Build the solution.
- [x] 7.4 Run `MiniBus.Core.Tests`, `MiniBus.AzureFunctions.Tests`, and any saga/persistence tests added by this change.
