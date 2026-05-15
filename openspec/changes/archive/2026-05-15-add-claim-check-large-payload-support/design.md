## Context

MiniBus currently serializes outgoing messages inline through the configured `IMessageSerializer`, maps MiniBus headers to Azure Service Bus application properties, and optionally captures outgoing operations in the SQL outbox before replaying them through the Azure Service Bus transport. Receive-side Azure Functions processing reads Service Bus headers, resolves the MiniBus message type, deserializes the received body, and then invokes handlers, sagas, persistence, and recoverability behaviors through the processing pipeline.

The Azure Storage persistence package now provides a Blob-backed `IMiniBusPayloadStore` that can write, read, and delete opaque payload bytes using MiniBus-owned payload references. The next step is to use that store as the first provider for optional claim-check/DataBus behavior: large serialized bodies are stored outside Service Bus messages, compact claim-check metadata travels over the transport, and receivers restore the original body before deserialization.

The design must preserve existing defaults. Applications that do not opt in to claim-check behavior should continue sending inline Service Bus messages and using SQL outbox replay exactly as they do today.

## Goals / Non-Goals

**Goals:**

- Add opt-in, threshold-based claim-check processing for outgoing `Send`, `Publish`, and `Schedule` operations.
- Use Azure Blob payload storage as the first payload provider without exposing Azure SDK types to handlers, message contracts, saga data, or `MiniBus.Core`.
- Resolve claim-checked bodies before deserialization so handlers and sagas receive normal message contracts.
- Preserve MiniBus message metadata across direct dispatch, scheduled dispatch, delayed retries, and SQL outbox replay.
- Route missing or invalid claim-check references through existing recoverability behavior with clear exceptions.
- Cover direct transport dispatch, receive-side resolution, and SQL outbox replay with automated tests.

**Non-Goals:**

- Do not add Table Storage inbox or saga persistence.
- Do not add automated payload cleanup beyond existing Blob payload expiry metadata.
- Do not add payload encryption, compression, chunking, or content-addressed storage in this change.
- Do not require all payloads to use claim-check storage; inline bodies remain the default for below-threshold messages and non-enabled applications.
- Do not introduce Azure SDK types into `MiniBus.Core`, handler APIs, saga APIs, or message contracts.

## Decisions

### Add MiniBus claim-check configuration as an opt-in feature

Claim-check behavior should be disabled by default and enabled through explicit configuration that supplies a payload threshold and a payload store provider. A practical shape is a MiniBus-owned DataBus/claim-check options object with a threshold in bytes and a provider hookup to the existing Azure Blob payload store registration.

Alternative considered: always claim-check messages when `MiniBus.Persistence.AzureStorage` is registered. That is surprising because a payload store can be useful for lower-level scenarios and because applications should choose when message wire format changes.

### Keep the wire format MiniBus-owned

Claim-checked messages should carry a compact MiniBus claim-check body plus MiniBus headers such as:

- `MiniBus.ClaimCheck.Enabled`
- `MiniBus.ClaimCheck.Provider`
- `MiniBus.ClaimCheck.ContainerName`
- `MiniBus.ClaimCheck.BlobName`
- `MiniBus.ClaimCheck.PayloadId`
- `MiniBus.ClaimCheck.PayloadLength`
- `MiniBus.ClaimCheck.ContentType`
- `MiniBus.ClaimCheck.CreatedUtc`
- `MiniBus.ClaimCheck.ExpiresUtc`

The body should be small and deterministic enough for tests, while headers provide fast receive-side detection and preserve enough metadata for delayed retries and outbox replay.

Alternative considered: use only a JSON body and no claim-check headers. That reduces header count, but makes delayed retry/header-only diagnostics weaker and forces body parsing before MiniBus can decide whether payload resolution is needed.

### Resolve claim-check payloads before deserialization

The Azure Functions processing pipeline should add a behavior between header/message-type resolution and message deserialization. This behavior detects claim-check metadata, validates the reference, reads the original payload bytes from the configured payload store, and replaces the processing body used by deserialization. The handler context and message metadata should continue to expose normal MiniBus headers, including correlation and causation metadata.

Alternative considered: teach every serializer to understand claim-check bodies. That spreads transport/storage concerns into serialization and makes custom serializer implementations responsible for MiniBus wire details.

### Apply claim-check processing before SQL outbox capture when outbox is enabled

Outgoing operations captured by SQL outbox should store the transport-ready body and headers after claim-check transformation. That means the large serialized body is already in Blob Storage, and the outbox row contains only the compact claim-check body plus metadata needed for deterministic replay.

Alternative considered: store original messages in the SQL outbox and claim-check only during outbox dispatch. That would keep business processing independent from Blob Storage, but it leaves large payloads in SQL outbox rows and makes replay behavior depend on dispatch-time storage availability rather than processing-time commit semantics.

### Preserve deterministic outgoing message IDs separately from payload IDs

SQL outbox replay already relies on deterministic outgoing message IDs. Claim-check payload IDs should not replace those IDs. Payload IDs may be generated by the payload store or derived from stable outbox/direct-dispatch context where useful, but Service Bus message identity remains the transport message identity.

Alternative considered: use outgoing message IDs as Blob payload IDs everywhere. That improves traceability but couples payload naming to transport identity and makes direct non-outbox sends less consistent when a caller does not provide stable IDs.

### Treat claim-check resolution failures as processing failures

Missing Blob payloads, malformed references, unsupported providers, and absent payload store registration should throw MiniBus claim-check exceptions during receive-side resolution. The existing Azure Functions recoverability pipeline should then apply immediate retry, delayed retry, or dead-letter decisions like any other processing failure.

Alternative considered: dead-letter immediately without using recoverability. That gives a clear terminal outcome, but it bypasses existing transient failure handling for storage/network failures.

## Risks / Trade-offs

- [Risk] Storing payloads before SQL transaction commit can orphan blobs when business processing later rolls back. → Mitigation: document that claim-check storage is durable before dispatch/outbox commit, store expiry metadata, and keep cleanup as future work.
- [Risk] Payload references can become a public wire contract too early. → Mitigation: keep the claim-check envelope MiniBus-owned, versioned, and Azure SDK independent.
- [Risk] Receive-side resolution adds storage latency before handler invocation. → Mitigation: make claim-check opt-in and threshold-based, and test the below-threshold inline path remains untouched.
- [Risk] Delayed retries copy compact claim-check bodies while payload blobs may expire. → Mitigation: require retention configuration to exceed retry windows and surface missing payloads through recoverability/dead-letter diagnostics.
- [Risk] Outbox replay after payload deletion can repeatedly fail. → Mitigation: preserve payload reference metadata in outbox rows and record dispatch failure metadata through existing outbox retry handling.

## Migration Plan

This change is additive and opt-in. Existing applications continue to send inline messages unless claim-check behavior is enabled. Applications adopting the feature should register Azure Blob payload storage, choose a threshold, and configure payload retention that exceeds their maximum processing, delayed retry, scheduled delivery, and outbox replay windows.

Rollback is disabling claim-check configuration for new outgoing messages. Already-sent claim-check messages still require receive-side claim-check resolution until they are drained or dead-lettered.

## Open Questions

- Should the claim-check configuration surface live in the Azure Storage package only for this first provider, or in a provider-neutral MiniBus options extension that Azure Storage plugs into?
- Should the compact claim-check body be JSON for inspectability or a minimal binary/string format for size?
- Should payload IDs be generated randomly by default, or should SQL outbox capture provide deterministic payload IDs for replay-safe duplicate storage attempts?
- Should the first implementation delete payload blobs after successful receive processing, or leave lifecycle management entirely to retention/cleanup policy?
