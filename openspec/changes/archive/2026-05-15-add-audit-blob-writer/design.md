## Context

MiniBus currently processes Azure Service Bus trigger messages through an internal Azure Functions pipeline. The pipeline carries received body bytes, headers, resolved message type, handler context, persistence state, recoverability decisions, and settlement decisions. Azure Blob Storage support already exists in `MiniBus.Persistence.AzureStorage` for payload/claim-check storage, with Azure SDK references isolated to that package and Azurite/live-resource-gated tests available.

The remaining Azure Storage backlog item is audit blob writing. Optional Azure Table Storage inbox and saga persistence have been deferred, so this design focuses on Blob Storage audit records only.

## Goals / Non-Goals

**Goals:**

- Provide opt-in audit writing for processed inbound messages.
- Keep handler-facing APIs and message contracts unchanged.
- Keep Azure SDK references out of `MiniBus.Core` contracts consumed by handlers.
- Capture enough metadata to inspect processed, skipped duplicate, delayed-retry, and dead-letter outcomes.
- Reuse the existing Azure Storage package, options style, and test infrastructure.
- Make audit write failure behavior explicit and test-covered.

**Non-Goals:**

- Azure Table Storage inbox or saga persistence.
- A manual retry dashboard or audit query UI.
- Full OpenTelemetry tracing, structured logging, or metrics.
- Automatic Azure infrastructure provisioning.
- Auditing outgoing messages as a separate feature.

## Decisions

### Use provider-neutral audit contracts plus Azure Blob implementation

Add a provider-neutral audit writer contract and audit record model that can be referenced by processing components without Azure SDK types. Implement Blob Storage persistence in `MiniBus.Persistence.AzureStorage`.

Alternative considered: put audit writing directly in the Azure Functions adapter. That would be smaller initially, but it would couple audit storage to Azure Functions processing and make future non-Functions processing reuse harder.

### Store JSON audit envelopes in Blob Storage

Audit records should be serialized as a stable JSON envelope containing metadata, headers, optional body bytes, and claim-check metadata when present. Blob metadata should hold lightweight indexing fields such as message id, message type, endpoint, outcome, created timestamp, and retention expiry when configured.

Alternative considered: store raw message bodies only. That is insufficient for operational inspection because the useful context is mostly in headers, message type metadata, correlation metadata, and outcome metadata.

### Use a separate audit container/prefix from payload storage

Extend `MiniBusAzureStoragePersistenceOptions` or related Azure Storage options with audit-specific container and prefix settings. Payload blobs and audit blobs must not rely on the same prefix because they have different retention and inspection semantics.

Alternative considered: use the existing payload container and prefix. That would reduce configuration, but it would blur retention and access-control boundaries between payload storage and audit storage.

### Invoke audit writing at the outcome boundary

Add audit invocation after MiniBus knows the processing outcome and before settlement completes a received message. Successful processing, duplicate inbox short-circuiting, delayed retry scheduling, and dead-letter decisions should be auditable. Immediate retry attempts should not create final audit records unless a later settled or propagated outcome is reached.

For no-settlement processing, audit successful processing before returning and propagate audit failures to the caller.

Alternative considered: add an audit pipeline behavior after handler/saga invocation only. That misses duplicate short-circuit and recoverability outcomes, and it cannot reliably capture final settlement decisions.

### Fail processing when enabled audit writing fails

When audit is enabled and the audit writer fails, MiniBus should treat the failure as a processing failure. For settlement-enabled processing, the received message must not be completed or dead-lettered as if auditing succeeded. For no-settlement processing, the audit failure propagates. This fail-closed behavior is safer for users who opt into audit storage for operational or compliance reasons.

Alternative considered: swallow audit failures or expose diagnostics only. That keeps message throughput moving, but it creates silent audit gaps and makes the feature less trustworthy.

### Preserve body semantics for inline and claim-checked messages

For inline messages, the audit envelope may contain the received or resolved body bytes encoded in a deterministic representation. For claim-checked messages, the envelope must include claim-check metadata and may include the resolved body only when configured to do so. The default should avoid duplicating large claim-checked payloads into audit blobs.

Alternative considered: always store resolved bodies. That is simple to inspect but can defeat the point of claim-check storage and surprise operators with high audit storage volume.

## Risks / Trade-offs

- Audit write outages can block settlement when audit is enabled -> document fail-closed behavior and keep auditing opt-in.
- Audit blobs may contain sensitive payloads or headers -> make body capture explicit, include retention metadata, and document access-control expectations.
- Blob listing is not a rich query store -> use date-partitioned names and blob metadata for light inspection; defer dashboards or query projections.
- Adding outcome-boundary hooks touches recoverability and settlement flow -> cover success, duplicate, delayed retry, dead-letter, no-settlement, and audit-failure paths in focused tests.

## Migration Plan

No existing application changes are required. Audit writing is disabled by default. Applications can opt in by registering the audit writer and configuring the audit Blob container/prefix. Rolling back means removing audit registration or disabling audit options; existing audit blobs can remain in storage until normal retention or manual cleanup.

## Open Questions

- Should the initial implementation include an option to include resolved claim-check bodies in audit envelopes, or should it store only claim-check references for claim-checked messages?
- Should audit records include handler and saga type names from the first implementation, or should that metadata be best-effort until observability work adds richer processing diagnostics?
