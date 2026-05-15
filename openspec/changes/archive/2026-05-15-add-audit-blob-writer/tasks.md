## 1. Audit Contracts and Record Model

- [x] 1.1 Add provider-neutral audit writer contracts and disabled/default behavior.
- [x] 1.2 Add audit record/envelope models for received message metadata, processing outcome, headers, body context, claim-check context, retry/dead-letter metadata, and timestamps.
- [x] 1.3 Add audit body capture options, including default handling for inline bodies and claim-checked payload metadata without large body duplication.
- [x] 1.4 Add serialization helpers for stable audit envelope JSON and deterministic timestamp/id formatting.

## 2. Azure Blob Audit Storage

- [x] 2.1 Extend Azure Storage persistence options with audit container, prefix, retention, audit id factory, audit clock, body capture, and audit BlobContainerClient factory configuration.
- [x] 2.2 Extend Azure Storage option validation and connection-string factory application for audit-specific configuration.
- [x] 2.3 Implement Blob-backed audit writer with safe date-partitioned blob names and collision-resistant audit ids.
- [x] 2.4 Write audit blob metadata for message id, endpoint, message type, processing outcome, audit timestamp, and expiry when available.
- [x] 2.5 Extend dependency injection registration so applications can opt into audit writing independently of payload/claim-check behavior.

## 3. Processing Integration

- [x] 3.1 Add processing context state needed to build audit records for success, duplicate, delayed retry, dead-letter, and no-settlement outcomes.
- [x] 3.2 Invoke audit writing for successful no-settlement processing before returning to the caller.
- [x] 3.3 Invoke audit writing for successful and duplicate settlement-enabled processing before completing the received Service Bus message.
- [x] 3.4 Invoke audit writing after delayed retry scheduling and before completing the original received Service Bus message.
- [x] 3.5 Invoke audit writing before dead-letter settlement and include dead-letter reason/description.
- [x] 3.6 Ensure audit writer failures fail processing and prevent final settlement when audit is enabled.
- [x] 3.7 Preserve existing processing behavior when no audit writer is configured.

## 4. Tests

- [x] 4.1 Add unit tests for audit options validation, audit id/blob name validation, retention metadata, and DI registration.
- [x] 4.2 Add unit tests for audit envelope serialization and metadata/body/claim-check selection.
- [x] 4.3 Add Azure Functions processing tests for success, duplicate, delayed retry, dead-letter, no-settlement success, and audit-disabled preservation.
- [x] 4.4 Add Azure Functions processing tests proving audit failures propagate and prevent completion/dead-letter settlement.
- [x] 4.5 Add Azurite-backed or live-resource-gated integration tests that write and read audit blobs and verify audit blob metadata.
- [x] 4.6 Run focused Azure Storage, Azure Functions, and core test suites.

## 5. Documentation and Backlog

- [x] 5.1 Document Blob audit writer registration, storage configuration, retention metadata, body capture behavior, and failure semantics.
- [x] 5.2 Update samples or README snippets only where they clarify opt-in audit registration without distracting from the existing sample.
- [x] 5.3 Update `openspec/project.md` Remaining feature backlog to mark audit blob writer complete.
- [x] 5.4 Validate the OpenSpec change status before implementation is considered ready.
