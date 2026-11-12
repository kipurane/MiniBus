## Context

MiniBus currently derives `MiniBus.MessageType` and `MiniBus.EnclosedMessageTypes` from `Type.AssemblyQualifiedName`. Receive-side processing and SQL outbox deserialization call `Type.GetType`, so durable records are coupled to the current CLR namespace, assembly name, and assembly-qualified representation. The same metadata crosses direct Azure Service Bus dispatch, scheduled messages, delayed retry copies, claim-check envelopes, SQL outbox rows, and audit/tooling records.

The change crosses `MiniBus.Core`, the Azure Service Bus transport, Azure Functions processing, SQL persistence, claim-check behavior, analyzers, samples, and documentation. Existing messages and applications must continue to work without requiring a coordinated migration.

## Goals / Non-Goals

**Goals:**

- Let applications assign an application-owned, stable wire identity to each MiniBus message contract.
- Use one transport-independent registry for outgoing identity selection and incoming identity resolution.
- Support receive-only aliases so an existing identity can follow a renamed or moved CLR contract.
- Preserve compatibility for applications and durable messages that still use assembly-qualified CLR names.
- Keep identity metadata consistent across direct dispatch, delayed retries, claim checks, and SQL outbox replay.
- Fail deterministically when configuration is invalid or an incoming identity cannot be resolved.

**Non-Goals:**

- Payload schema versions, JSON transformation, or upcasting.
- Renaming broker entities or changing command/event routing semantics.
- Rewriting existing SQL outbox rows, scheduled messages, or claim-check blobs.
- Discovering message identities from broker topology or conventions.
- Replacing `IMessageSerializer` or changing its concrete `Type`-based contract.

## Decisions

### Add a transport-independent identity registry in MiniBus.Core

Introduce a public registry, conceptually `MiniBusMessageIdentityRegistry`, with generic registration for a primary identity and optional receive aliases. It provides two operations: select the outgoing identity for a CLR `Type`, and resolve an incoming identity to a CLR `Type`. Registrations are restricted to MiniBus message contract types.

The registry owns no Azure SDK or dependency-injection types. Azure Functions registration exposes the registry through processor options and registers that same instance as a singleton so the processor, transport factory, claim-check transformer, and SQL serializer can consume consistent mappings. Transport-only applications can construct and pass the registry explicitly.

Alternative considered: place identity mappings on `AzureServiceBusTransportRoutes`. That would couple a durable serialization concern to one transport and would not naturally cover SQL deserialization or future transports.

### Treat primary identities as outgoing values and aliases as receive-only values

Each CLR message type has at most one primary stable identity. Both the primary identity and aliases resolve to that type, but only the primary identity is written to new messages and outbox rows. Identity comparison is ordinal and case-sensitive. Null, empty, whitespace-only, repeated, or cross-type conflicting values fail during registration rather than at first dispatch.

Aliases are intentionally receive-only so configuration order cannot change which value appears on the wire. An old assembly-qualified name can be registered as an alias after a CLR rename, allowing already durable messages to resolve to the replacement type.

Alternative considered: permit multiple outgoing names with a preferred flag. This adds mutable preference and rollout ambiguity without helping the initial compatibility case.

### Preserve the current CLR-name fallback in both directions

When a type has no stable registration, outgoing behavior continues to use its assembly-qualified name. Incoming resolution first checks registered primary identities and aliases, then uses the existing `Type.GetType` fallback. A fallback result is accepted only when it represents a MiniBus message contract. Unknown identities still raise the existing processing or outbox resolution error path.

This makes adoption incremental and prevents the feature from becoming an immediate breaking change. It also means stable identity adoption is explicit rather than silently changing existing wire contracts.

Alternative considered: require every contract to be registered. That gives stronger uniformity but would break every current application and make partial rollout across endpoints difficult.

### Use the same selected identity in all durable metadata

The selected outgoing identity populates `MiniBus.MessageType` and `MiniBus.EnclosedMessageTypes`. SQL outbox serialization stores that identity in the `MessageType` column and in the serialized header set. SQL deserialization resolves the stored identity through the registry before reconstructing the `MiniBusOutboxStoredOperation`. No schema change is needed because the existing column already stores a string with sufficient capacity.

Claim-check transformation uses the same identity selection path before deciding whether the body is inline or external. Delayed retries copy the received identity unchanged. Persisted outbox dispatch does not recompute a different wire identity from the reconstructed CLR type; it preserves the stored message-type header, while the resolved CLR type remains available for routing.

Alternative considered: leave the SQL `MessageType` column as a CLR name and change only transport headers. That would leave outbox rows vulnerable to precisely the contract rename this feature is intended to support and could produce mismatched metadata during replay.

### Keep CLR type resolution separate from payload evolution

The registry resolves a wire identity to the current CLR type, after which the existing `IMessageSerializer.Deserialize(BinaryData, Type)` path remains unchanged. Stable identity does not imply that an incompatible historical JSON body can deserialize into the current contract.

This boundary keeps the first feature small and establishes the correct seam for a later version-header and upcaster capability without prematurely defining transformation ordering or version semantics.

### Add analyzer diagnostics only for statically provable mistakes

The analyzer reports invalid literal identities and conflicting literal registrations visible in the same compilation. Dynamic values, registrations in external assemblies, or composition split across methods do not produce speculative completeness diagnostics. Runtime validation remains authoritative.

Alternative considered: require every visible `IMessage` type to have a stable identity. Registration can legitimately be external or incremental, so that rule would create false positives and contradict the compatibility fallback.

## Risks / Trade-offs

- **[Risk] Different endpoint processes configure different mappings for the same contract.** → Document shared registration through a contracts/configuration package and cover sample multi-endpoint consistency; unknown or conflicting local mappings fail clearly.
- **[Risk] A stable identity is changed without retaining the old value as an alias.** → Document identity immutability and rename procedure, and provide analyzer/runtime collision checks; aliases make deliberate migration possible.
- **[Risk] Stable identity is mistaken for payload compatibility.** → State explicitly in API documentation and failures that identity resolution does not upcast incompatible bodies.
- **[Risk] Direct dispatch and SQL replay select different identities.** → Pass one registry through all serializers and preserve stored headers during replay; add cross-path tests for identical metadata.
- **[Trade-off] Fallback behavior allows applications to postpone stable registration.** → This is necessary for compatibility; documentation and samples will recommend stable identities for durable production contracts.
- **[Trade-off] Case-sensitive identities can create operational mistakes.** → Ordinal matching avoids culture-dependent behavior and accidental aliasing; clear unknown-identity diagnostics expose casing errors.

## Migration Plan

1. Add the registry and compatibility fallback without changing existing application configuration.
2. Thread the singleton registry through Azure Functions, Azure Service Bus message creation, claim-check transformation, and SQL outbox serialization/deserialization.
3. Add stable identities to repository samples and templates, retaining any previous durable names as aliases where a rename example is useful.
4. Deploy consumers that can resolve both stable identities and legacy CLR names before producers begin writing stable identities when endpoints are released independently.
5. After all consumers are compatible, deploy producer configuration that selects stable identities for new messages.

Rollback is configuration-safe: removing a stable registration returns new outgoing messages to the CLR-name fallback. Messages already written with a stable identity still require the mapping or alias to remain available, so rollback across independently deployed endpoints must retain receive mappings until those messages drain.

## Open Questions

None required before implementation. Payload versioning and upcaster ordering should be designed as a separate change after stable identity behavior is established.
