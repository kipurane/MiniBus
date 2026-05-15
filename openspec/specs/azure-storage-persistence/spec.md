# azure-storage-persistence Specification

## Purpose
Defines Azure Storage persistence registration and Blob-backed payload store behavior for MiniBus.

## Requirements

### Requirement: Azure Storage persistence package isolates Azure SDK dependencies
MiniBus SHALL provide an Azure Storage persistence package for Blob-backed payload storage without requiring application handlers, message contracts, saga data, or `MiniBus.Core` to reference Azure SDK types.

#### Scenario: Handlers remain Azure SDK independent
- **WHEN** an application enables Azure Storage payload persistence
- **THEN** handlers continue to depend only on MiniBus message contracts and `MiniBusContext`

#### Scenario: Core remains Azure SDK independent
- **WHEN** MiniBus core contracts are compiled
- **THEN** they do not require Azure Storage SDK references

### Requirement: Azure Storage persistence is configured through dependency injection
MiniBus SHALL provide dependency injection registration for Blob-backed payload storage, including connection configuration, container name, optional blob name prefix, and payload retention metadata options.

#### Scenario: Application registers Blob payload storage with a connection string
- **WHEN** an application registers MiniBus Azure Storage persistence with a Blob Storage connection string and container name
- **THEN** MiniBus can resolve the configured payload store using Azure Blob Storage

#### Scenario: Application registers Blob payload storage with a client factory
- **WHEN** an application registers MiniBus Azure Storage persistence with a caller-provided Blob service or container client factory
- **THEN** MiniBus uses the provided factory for payload store operations

#### Scenario: Required configuration is missing
- **WHEN** an application registers Blob payload storage without required connection or container configuration
- **THEN** MiniBus rejects the configuration with an actionable error before payload operations run

### Requirement: Blob payload store writes payloads
MiniBus SHALL provide a Blob-backed payload store that writes opaque payload bytes to the configured container and returns a MiniBus-owned payload reference.

#### Scenario: Payload is written
- **WHEN** MiniBus stores a payload through the Blob payload store
- **THEN** the store writes the payload bytes to Azure Blob Storage and returns a reference containing the container name, blob name, payload length, and creation timestamp

#### Scenario: Payload content type is provided
- **WHEN** MiniBus stores a payload with a content type
- **THEN** the Blob payload store records that content type on the blob or returned payload reference

#### Scenario: Payload expiry is configured
- **WHEN** MiniBus stores a payload with an expiry time or retention option
- **THEN** the Blob payload store records expiry metadata that future cleanup behavior can inspect

### Requirement: Blob payload store names payloads safely
MiniBus SHALL generate collision-resistant blob names under the configured prefix unless a caller provides an explicit payload identifier for deterministic storage.

#### Scenario: Store generates payload name
- **WHEN** MiniBus stores a payload without an explicit payload identifier
- **THEN** the Blob payload store creates a blob name that is unique within the configured container and prefix

#### Scenario: Caller provides payload identifier
- **WHEN** MiniBus stores a payload with an explicit payload identifier
- **THEN** the Blob payload store uses that identifier in the blob name according to the configured naming rules

#### Scenario: Invalid payload identifier is rejected
- **WHEN** a caller provides a payload identifier that cannot be represented safely as a blob name
- **THEN** MiniBus rejects the request before writing payload bytes

### Requirement: Blob payload store reads payloads
MiniBus SHALL read stored payload bytes by using the MiniBus-owned payload reference returned from a previous write.

#### Scenario: Payload is read
- **WHEN** MiniBus reads a payload using a valid payload reference
- **THEN** the Blob payload store returns the exact bytes that were stored for that reference

#### Scenario: Payload reference is missing
- **WHEN** MiniBus reads a payload reference whose blob does not exist
- **THEN** the Blob payload store reports a clear payload-not-found failure

#### Scenario: Payload reference targets invalid storage
- **WHEN** MiniBus reads a malformed or incompatible payload reference
- **THEN** the Blob payload store rejects the reference before attempting to deserialize a message body from it

### Requirement: Blob payload store deletes payloads
MiniBus SHALL delete stored payload blobs by payload reference and make repeated delete attempts safe.

#### Scenario: Payload is deleted
- **WHEN** MiniBus deletes a payload using a valid payload reference
- **THEN** the Blob payload store removes the referenced blob from Azure Blob Storage

#### Scenario: Payload is already absent
- **WHEN** MiniBus deletes a payload reference whose blob is already absent
- **THEN** the Blob payload store treats the delete as successful

### Requirement: Azure Storage payload behavior is documented and tested
MiniBus SHALL document and test Azure Storage payload registration and Blob payload store behavior.

#### Scenario: Documentation shows setup
- **WHEN** a developer reads MiniBus Azure Storage persistence documentation
- **THEN** it shows package registration, Blob Storage connection configuration, container configuration, and the current scope of payload storage versus future claim-check processing

#### Scenario: Unit tests cover payload store behavior
- **WHEN** the normal test suite runs
- **THEN** it verifies option validation, payload reference creation, blob name validation, and Azure SDK-independent contract behavior without requiring Azure Storage infrastructure

#### Scenario: Integration tests cover Blob Storage behavior with Testcontainers
- **WHEN** Azure Storage-backed integration tests run with Docker/Testcontainers available
- **THEN** they start an isolated Azurite container and verify payload write, read, delete, metadata, and missing-payload behavior against Blob Storage-compatible infrastructure

#### Scenario: Integration tests use live storage fallback
- **WHEN** Azure Storage-backed integration tests run with a documented live Azure Storage connection string
- **THEN** they may use that storage account instead of Testcontainers-backed Azurite to verify payload store behavior

#### Scenario: Storage infrastructure is unavailable
- **WHEN** Azure Storage-backed integration tests run without Docker/Testcontainers availability and without a live storage connection string
- **THEN** the tests are skipped with a clear reason without failing the normal test run

### Requirement: Azure Storage persistence configures Blob audit storage
MiniBus SHALL provide Azure Storage persistence registration for Blob-backed audit writing without exposing Azure SDK types to handlers, message contracts, saga data, or handler-facing MiniBus APIs.

#### Scenario: Application registers Blob audit storage with a connection string
- **WHEN** an application registers MiniBus Azure Storage audit writing with a Blob Storage connection string and audit container name
- **THEN** MiniBus can resolve the configured audit writer using Azure Blob Storage

#### Scenario: Application registers Blob audit storage with a client factory
- **WHEN** an application registers MiniBus Azure Storage audit writing with a caller-provided Blob container client factory
- **THEN** MiniBus uses the provided factory for audit blob operations

#### Scenario: Required audit configuration is missing
- **WHEN** an application enables Blob audit writing without required connection or container configuration
- **THEN** MiniBus rejects the configuration with an actionable error before audit operations run

### Requirement: Blob audit writer writes audit envelopes
MiniBus SHALL provide a Blob-backed audit writer that writes serialized audit envelopes to the configured audit container.

#### Scenario: Audit envelope is written
- **WHEN** MiniBus stores an audit record through the Blob audit writer
- **THEN** the writer creates an audit blob containing the serialized audit envelope

#### Scenario: Audit metadata is written
- **WHEN** MiniBus stores an audit record through the Blob audit writer
- **THEN** the writer records lightweight blob metadata for message id, endpoint name, message type, processing outcome, audit timestamp, and expiry timestamp when those values are available

#### Scenario: Audit expiry is configured
- **WHEN** MiniBus stores an audit record and audit retention is configured
- **THEN** the writer records expiry metadata that future cleanup behavior can inspect

### Requirement: Blob audit writer names audit blobs safely
MiniBus SHALL generate collision-resistant audit blob names under the configured audit prefix.

#### Scenario: Audit writer generates blob name
- **WHEN** MiniBus stores an audit record without an explicit audit id
- **THEN** the writer creates a blob name that is unique within the configured audit container and prefix

#### Scenario: Audit blob name is date partitioned
- **WHEN** MiniBus stores an audit record
- **THEN** the generated blob name includes a UTC date partition based on the audit timestamp

#### Scenario: Invalid audit id is rejected
- **WHEN** a caller-provided audit id cannot be represented safely as a blob name segment
- **THEN** MiniBus rejects the audit write before writing bytes

### Requirement: Azure Storage audit behavior is tested
MiniBus SHALL test Blob-backed audit writing with unit coverage and Azure Storage-compatible integration coverage.

#### Scenario: Unit tests cover audit storage behavior
- **WHEN** the normal test suite runs
- **THEN** it verifies audit options validation, envelope serialization, blob name validation, metadata creation, and Azure SDK-independent audit contracts without requiring Azure Storage infrastructure

#### Scenario: Integration tests cover Blob audit writing
- **WHEN** Azure Storage-backed integration tests run with Docker/Testcontainers available
- **THEN** they start isolated Azurite infrastructure and verify audit blob write and read behavior against Blob Storage-compatible infrastructure

#### Scenario: Integration tests use live storage fallback
- **WHEN** Azure Storage-backed integration tests run with a documented live Azure Storage connection string
- **THEN** they may use that storage account instead of Testcontainers-backed Azurite to verify audit blob behavior

#### Scenario: Storage infrastructure is unavailable
- **WHEN** Azure Storage-backed integration tests run without Docker/Testcontainers availability and without a live Azure Storage connection string
- **THEN** the tests are skipped with a clear reason without failing the normal test run
