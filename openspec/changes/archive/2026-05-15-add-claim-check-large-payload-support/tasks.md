## 1. Claim-Check Contracts and Configuration

- [x] 1.1 Add MiniBus-owned claim-check header names, provider identifiers, and compact envelope/reference types without introducing Azure SDK types into handler-facing APIs.
- [x] 1.2 Add opt-in claim-check/DataBus configuration with payload threshold bytes and validation for missing or invalid provider/store setup.
- [x] 1.3 Wire Azure Blob payload storage as the first claim-check provider using the existing Blob payload store registration.
- [x] 1.4 Add unit tests for disabled-by-default behavior, threshold validation, provider validation, and Azure SDK isolation.

## 2. Outgoing Claim-Check Transformation

- [x] 2.1 Add an outgoing payload transformation service that serializes messages, compares serialized length with the configured threshold, and returns either inline body metadata or claim-check body metadata.
- [x] 2.2 Store above-threshold serialized bodies through the configured payload store and build MiniBus claim-check headers with provider, payload reference, content type, length, created, and expiry metadata.
- [x] 2.3 Preserve message type, enclosed message types, content type, message id, correlation id, and causation id while adding claim-check metadata.
- [x] 2.4 Add unit tests for below-threshold send/publish/schedule staying inline and above-threshold send/publish/schedule producing compact claim-check bodies.

## 3. Azure Service Bus Dispatch Integration

- [x] 3.1 Update Service Bus message creation for direct send, publish, and schedule paths to use the outgoing transformation result.
- [x] 3.2 Ensure Service Bus application properties include MiniBus claim-check headers and system properties still mirror message id, correlation id, content type, and subject where supported.
- [x] 3.3 Update persisted outbox dispatch paths to send stored compact bodies and stored claim-check headers without reserializing original message payloads.
- [x] 3.4 Add transport tests for claim-checked direct command sends, event publishes, scheduled messages, and persisted outbox dispatch operations.

## 4. Receive-Side Claim-Check Resolution

- [x] 4.1 Add a pipeline behavior before message deserialization that detects MiniBus claim-check metadata.
- [x] 4.2 Validate claim-check metadata and load the original serialized payload bytes from the configured payload store before deserialization.
- [x] 4.3 Keep inline messages on the existing deserialization path when no claim-check metadata is present.
- [x] 4.4 Add processing tests proving resolved claim-check messages deserialize into the original handler/saga message contract and preserve `MiniBusContext` metadata.

## 5. Recoverability and Retry Behavior

- [x] 5.1 Add clear exceptions for missing payloads, invalid claim-check references, unsupported providers, and missing provider configuration.
- [x] 5.2 Ensure claim-check resolution failures occur before handler invocation and flow through existing immediate retry, delayed retry, dead-letter, and propagation decisions.
- [x] 5.3 Ensure delayed retry scheduling preserves compact claim-check bodies and MiniBus claim-check metadata while adding retry headers.
- [x] 5.4 Add recoverability tests for missing payload references, invalid metadata, delayed retry preservation, and exhausted retry dead-letter behavior.

## 6. SQL Outbox Capture and Replay

- [x] 6.1 Ensure SQL outbox capture stores claim-check transformed operations containing compact bodies, claim-check headers, deterministic outgoing message ids, and schedule due times.
- [x] 6.2 Verify replayed claim-check operations use the same stored claim-check metadata and deterministic outgoing message id across repeated dispatch attempts.
- [x] 6.3 Ensure dispatch failures for claim-checked outbox operations record retry metadata through existing outbox failure handling.
- [x] 6.4 Add SQL outbox tests for claim-checked send, publish, schedule capture, replay, repeated replay, and failure metadata.

## 7. Documentation and Backlog

- [x] 7.1 Document claim-check setup with Azure Blob payload storage, threshold configuration, retention guidance, and round-trip behavior.
- [x] 7.2 Document operational caveats for blob retention, delayed retry windows, scheduled delivery, SQL outbox replay, and orphaned payload cleanup.
- [x] 7.3 Update the billing sample or README snippets only if a concise configuration example improves developer understanding without making claim-check required.
- [x] 7.4 Update `openspec/project.md` Remaining feature backlog to mark large payload/DataBus/claim-check support and receive-side claim-check resolution according to the implemented scope.

## 8. Verification

- [x] 8.1 Run targeted unit tests for core claim-check configuration, outgoing transformation, Azure Service Bus transport mapping, Azure Functions processing, and recoverability.
- [x] 8.2 Run Azure Storage-backed tests for Blob payload write/read behavior used by claim-check resolution.
- [x] 8.3 Run SQL persistence/outbox tests that cover claim-checked capture and replay.
- [x] 8.4 Run `dotnet test` for the solution or document any infrastructure-gated tests that could not run locally.
