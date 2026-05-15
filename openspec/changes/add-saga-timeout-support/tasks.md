## 1. Core Saga Timeout API

- [x] 1.1 Add a transport-independent saga timeout message marker contract in `MiniBus.Core`.
- [x] 1.2 Add saga-facing timeout request helpers that schedule absolute due times through the current `MiniBusContext`.
- [x] 1.3 Add saga-facing timeout request helpers that accept relative delays and convert them to future due times.
- [x] 1.4 Validate timeout request inputs before scheduling outgoing work.

## 2. Saga Correlation and Invocation

- [x] 2.1 Add core tests showing timeout messages use existing continuing saga correlation rules.
- [x] 2.2 Add core tests showing timeout messages do not create saga state unless explicitly mapped as starting messages.
- [x] 2.3 Add core tests showing completed sagas ignore delivered timeout messages.
- [x] 2.4 Add core tests showing timeout handler failures preserve prior saga state and flow through normal failure behavior.

## 3. Scheduling and Persistence Behavior

- [x] 3.1 Add Azure Functions processing tests showing timeout requests use the existing direct scheduled dispatch path when outbox capture is disabled.
- [x] 3.2 Add Azure Functions processing tests showing timeout requests are captured as scheduled outbox operations when outbox capture is enabled.
- [x] 3.3 Add SQL persistence tests verifying saga timeout schedules persist as outbox `Schedule` operations with due time, message type, body, and headers.
- [x] 3.4 Add SQL Server integration coverage verifying saga state changes and timeout schedule capture commit together after successful handling.
- [x] 3.5 Add SQL Server integration coverage verifying failed saga handling does not commit requested timeout schedules.

## 4. Azure Service Bus Dispatch

- [x] 4.1 Add transport tests showing direct timeout scheduling calls the Azure Service Bus sender with the configured destination and due time.
- [x] 4.2 Add transport tests showing persisted timeout outbox operations are scheduled with stored headers and deterministic outgoing message ids.
- [x] 4.3 Ensure timeout messages use existing scheduled-message route resolution without adding timeout-specific Service Bus SDK dependencies to handlers.

## 5. Documentation and Project Backlog

- [x] 5.1 Update saga documentation or samples to show defining, requesting, correlating, and handling a timeout message.
- [x] 5.2 Document that this change uses Service Bus scheduled messages and does not add a SQL timeout table.
- [x] 5.3 Update `openspec/project.md` Remaining feature backlog to mark saga timeout APIs, Service Bus timeout decision, and timeout dispatch tests according to implemented scope.
- [x] 5.4 Run the relevant unit and SQL integration test suites.
