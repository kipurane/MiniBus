## Why

Azure Service Bus messages should stay small, but MiniBus currently serializes every outgoing message body inline. The Azure Storage payload store now provides the missing storage foundation, so MiniBus can add a full claim-check/DataBus round trip that keeps large payloads out of transport messages while preserving transparent handler behavior.

## What Changes

- Add optional threshold-based claim-check processing for outgoing `Send`, `Publish`, and `Schedule` operations.
- Store serialized payload bodies larger than the configured threshold through the Azure Blob-backed payload store.
- Replace large outgoing transport bodies with a compact MiniBus claim-check representation and MiniBus claim-check metadata headers.
- Resolve claim-checked receive bodies before message deserialization so handlers and sagas receive the original message contract without Azure Storage dependencies.
- Preserve message type, content type, message id, correlation id, causation id, scheduled delivery, delayed retry, and SQL outbox replay metadata across claim-check processing.
- Surface missing or invalid claim-check references as clear processing failures that flow through normal recoverability.
- Add automated coverage for inline payloads, claim-checked direct dispatch, claim-checked scheduled dispatch, receive-side resolution, SQL outbox capture/replay, retry metadata preservation, and failure behavior.
- Update the remaining feature backlog after implementation to reflect the completed large payload/DataBus/claim-check and receive-side resolution scope.

## Capabilities

### New Capabilities

- `large-payload-claim-check`: Defines optional MiniBus DataBus/claim-check behavior for threshold-based outgoing payload storage, compact wire envelopes, receive-side payload resolution, and provider-independent handler semantics with Azure Blob Storage as the first provider.

### Modified Capabilities

- `azure-servicebus-transport`: Outgoing Service Bus message creation and persisted outbox dispatch must support claim-checked bodies and MiniBus claim-check headers while preserving existing routing and metadata behavior.
- `azure-functions-adapter`: Receive-side processing must resolve claim-checked payloads before deserialization and route invalid or missing payload references through recoverability.
- `sql-inbox-outbox`: SQL outbox capture and replay must preserve claim-check wire bodies and metadata so large outgoing operations remain replay-safe.
- `basic-recoverability`: Delayed retry scheduling and failure handling must preserve claim-check metadata and report claim-check resolution failures through existing retry/dead-letter decisions.

## Impact

- Adds DataBus/claim-check configuration APIs and processing services, with Azure Blob-backed payload storage as the first implementation.
- Updates Azure Service Bus message factory/dispatch paths to choose between inline serialized bodies and claim-check envelopes.
- Updates Azure Functions processing pipeline to resolve claim-check references before deserialization and handler invocation.
- Extends SQL outbox tests and transport replay coverage for claim-checked operations.
- Adds unit/integration tests that may combine existing Azure Storage Azurite coverage with transport and SQL outbox fakes/Testcontainers.
- No breaking changes are intended; claim-check behavior is opt-in and existing inline message behavior remains the default.
