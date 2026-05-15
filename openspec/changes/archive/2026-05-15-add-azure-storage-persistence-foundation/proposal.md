## Why

MiniBus plans to support large payload and Azure Storage-backed persistence, but there is not yet an Azure Storage package or a concrete storage contract to build on. Starting with a Blob-backed payload store gives the framework a useful first Azure Storage slice and establishes the foundation for future claim-check/DataBus processing without changing receive-side message behavior yet.

## What Changes

- Create `MiniBus.Persistence.AzureStorage` or an equivalent package for Azure Storage persistence infrastructure.
- Add dependency injection and configuration primitives for registering Azure Blob Storage-backed payload persistence.
- Introduce a Blob-backed payload store abstraction that can write, read, and delete serialized payload bytes by stable payload identity.
- Define payload naming, container usage, content metadata, expiry metadata, and error behavior for missing or invalid payload references.
- Add automated coverage for payload write/read/delete behavior using unit tests plus Testcontainers-backed Azurite integration tests, with an optional live Azure Storage connection-string fallback.
- Keep full claim-check message processing, receive-side automatic payload resolution, Table Storage inbox, Table Storage saga persistence, and audit blob writing out of scope for this change.

## Capabilities

### New Capabilities

- `azure-storage-persistence`: Defines Azure Storage persistence registration and Blob-backed payload store behavior for MiniBus.

### Modified Capabilities

- None.

## Impact

- Adds a new Azure Storage persistence project/package and test project coverage.
- Adds Azure Storage Blob SDK dependencies to the Azure Storage package only.
- Adds public registration/configuration APIs for applications that want to use Blob Storage as MiniBus payload storage.
- Establishes payload storage contracts that future claim-check/DataBus work can consume without requiring handlers or message contracts to reference Azure SDK types.
