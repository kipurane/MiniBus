## 1. Project Setup

- [x] 1.1 Add `src/MiniBus.Persistence.Sql` project targeting the solution's .NET version and reference `MiniBus.Core`.
- [x] 1.2 Add `tests/MiniBus.Persistence.Sql.Tests` and include it in `MiniBus.sln`.
- [x] 1.3 Keep concrete SQL client dependencies out of core and require applications to provide the `DbConnection` factory for this slice.

## 2. Core Persistence Abstractions

- [x] 2.1 Add core inbox abstractions for checking and recording processed logical message ids by endpoint.
- [x] 2.2 Add core outbox abstractions for capturing outgoing send, publish, and schedule operations.
- [x] 2.3 Add core outbox dispatch abstractions that allow persisted operations to be dispatched through the configured transport.
- [x] 2.4 Add option types for enabling inbox/outbox behavior without changing default direct-dispatch behavior.

## 3. SQL Schema and Store

- [x] 3.1 Add initial SQL schema scripts for inbox and outbox tables, indexes, status columns, timestamps, retry metadata, and stored headers/body fields.
- [x] 3.2 Implement SQL inbox lookup and processed-message recording using endpoint name plus logical message id.
- [x] 3.3 Implement SQL outbox operation persistence for send, publish, and schedule requests.
- [x] 3.4 Implement atomic commit behavior for inbox records and captured outbox operations.
- [x] 3.5 Add dependency injection registration for SQL connection settings, inbox services, outbox services, and dispatcher services.

## 4. Processor Integration

- [x] 4.1 Update Azure Functions processing to detect duplicate inbox messages before handler invocation when SQL persistence is enabled.
- [x] 4.2 Update `MiniBusContext` processing behavior to capture outgoing operations for SQL outbox persistence when enabled.
- [x] 4.3 Commit SQL inbox and outbox state after successful handler execution and before completing the received Service Bus message.
- [x] 4.4 Preserve existing direct-dispatch behavior when SQL persistence is not enabled.
- [x] 4.5 Ensure SQL commit failures propagate without completing the incoming Service Bus message.

## 5. Outbox Dispatcher

- [x] 5.1 Implement bounded batch claiming for pending outbox rows with concurrency-safe status updates.
- [x] 5.2 Rehydrate persisted outbox operations with serialized body, message type metadata, headers, operation kind, and due time.
- [x] 5.3 Dispatch claimed operations through the existing Azure Service Bus transport dispatch path.
- [x] 5.4 Mark successful operations as dispatched with a timestamp.
- [x] 5.5 Record failed dispatch attempts and last error details while leaving operations retryable.

## 6. Tests and Documentation

- [x] 6.1 Add SQL persistence tests for duplicate detection and logical message id selection using `MiniBus.OriginalMessageId`.
- [x] 6.2 Add SQL persistence tests for outbox capture and transactional commit behavior.
- [x] 6.3 Add dispatcher tests for claiming, successful dispatch, failed dispatch metadata, and batch limits.
- [x] 6.4 Add Azure Functions processor tests for SQL-backed duplicate handling, commit-before-complete, commit failure, and direct-dispatch fallback.
- [x] 6.5 Add transport tests for dispatching persisted outbox operations with preserved MiniBus headers.
- [x] 6.6 Document SQL schema setup, dependency injection registration, processing semantics, outbox dispatcher usage, and at-least-once dispatch caveats.
