## Why

MiniBus currently writes assembly-qualified CLR type names into durable message metadata and resolves them with runtime type loading. Namespace, assembly, or contract type renames can therefore make queued, scheduled, claim-checked, or persisted outbox messages unresolvable after a deployment, so MiniBus needs stable application-owned wire identities before those identifiers become a long-lived compatibility constraint.

## What Changes

- Add a transport-independent registry that maps a MiniBus message contract type to one stable logical wire identity and maps accepted wire identities back to the current CLR type.
- Allow applications to register explicit stable identities and legacy aliases, with deterministic validation for empty, duplicate, or conflicting registrations.
- Write the registered stable identity into outgoing message type metadata while retaining the current assembly-qualified identity as the compatibility fallback for unregistered contracts.
- Resolve incoming stable identities and registered aliases before falling back to the existing CLR type resolution behavior, preserving compatibility with messages already in queues, schedules, claim-check storage, and SQL outbox rows.
- Preserve stable message identities unchanged through Azure Service Bus envelopes, delayed retries, claim-check round trips, and persisted outbox dispatch.
- Add high-confidence analyzer guidance for statically visible duplicate or invalid stable identity registrations without requiring analyzers for runtime correctness.
- Document identity registration, compatibility behavior, contract rename guidance, and the fact that payload schema upcasting is not introduced by this change.

## Capabilities

### New Capabilities

- `stable-message-identities`: Defines registration, outgoing identity selection, incoming identity resolution, aliases, validation, and backward-compatible fallback behavior for durable MiniBus message metadata.

### Modified Capabilities

- `minibus-core`: Message metadata resolution gains a transport-independent stable identity registry while the serializer continues to receive a concrete CLR `Type`.
- `azure-servicebus-transport`: Azure Service Bus envelopes and persisted outbox dispatch use and preserve the identity selected by the core registry.
- `azure-functions-adapter`: Receive-side processing resolves registered stable identities and aliases before deserialization and routes unknown identities through existing failure handling.
- `sql-inbox-outbox`: Persisted outbox operations store and resolve stable identities while remaining able to dispatch rows written with legacy CLR type names.
- `large-payload-claim-check`: Claim-checked messages preserve and resolve the same stable identity as inline messages.
- `roslyn-analyzers`: Compile-time analysis covers statically visible invalid or conflicting stable identity registrations.

## Impact

- Affects `MiniBus.Core` message metadata APIs and registration, Azure Service Bus envelope creation, Azure Functions receive-side type resolution, claim-check metadata, SQL outbox serialization/replay compatibility, analyzers, dependency injection setup, documentation, and tests.
- Adds public configuration APIs for identity registration and resolution; existing applications remain valid without configuration through the current assembly-qualified-name fallback.
- Does not change message body serialization, introduce payload version headers or upcasters, provision broker topology, or rewrite already persisted messages.
