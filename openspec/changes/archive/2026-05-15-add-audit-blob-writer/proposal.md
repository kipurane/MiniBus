## Why

MiniBus can already persist large payloads in Azure Blob Storage, but processed inbound messages currently disappear once handler processing and settlement finish. An opt-in audit blob writer gives operators a durable inspection trail for processed messages while keeping Azure SDK dependencies out of handlers and core messaging contracts.

## What Changes

- Add opt-in Blob Storage-backed audit writing for inbound MiniBus processing.
- Define provider-neutral audit contracts and audit record/envelope shape that do not expose Azure SDK types to handlers or `MiniBus.Core`.
- Extend Azure Storage persistence registration with audit blob configuration, including container, blob name prefix, retention metadata, clock/id factories, and caller-provided `BlobContainerClient` factory support.
- Write audit records after successful processing and terminal processing outcomes when MiniBus has enough received-message context to describe what happened.
- Capture useful audit metadata: message id, correlation id, causation id, message type, endpoint/source metadata when available, handler/saga metadata when available, processing outcome, timestamps, headers, and inline body or claim-check metadata as appropriate.
- Add explicit, tested failure behavior for audit write failures.
- Add unit and Azure Storage integration coverage for options validation, audit envelope serialization, safe blob naming, metadata selection, SDK isolation, and Blob-backed write/read verification.
- Update the remaining feature backlog after implementation to mark audit blob writer complete.

## Capabilities

### New Capabilities
- `audit-blob-writer`: Defines opt-in MiniBus audit record contracts and Azure Blob Storage audit writing behavior for processed inbound messages.

### Modified Capabilities
- `azure-storage-persistence`: Azure Storage persistence registration and Blob behavior expand from payload storage to include configured audit blob storage.
- `core-processing-pipeline`: The processing pipeline gains an audit hook that observes completed or terminal processing outcomes without changing handler-facing behavior.
- `azure-functions-adapter`: Azure Functions processing supplies received Service Bus metadata needed by audit records and preserves existing settlement behavior when auditing is disabled.

## Impact

- Adds provider-neutral audit abstractions and audit record models, likely in `MiniBus.Core` or an existing non-Azure-facing boundary.
- Extends `MiniBus.Persistence.AzureStorage` with Blob-backed audit writer implementation, options, validation, DI registration, and Azure Blob SDK usage isolated to that package.
- Updates Azure Functions processing pipeline behavior to invoke auditing at the appropriate outcome point.
- Extends `MiniBus.Persistence.AzureStorage.Tests` and `MiniBus.AzureFunctions.Tests` with unit and Azurite/live-resource-gated integration coverage.
- No breaking changes are intended; audit writing is opt-in and existing processing behavior remains unchanged when auditing is not registered.
