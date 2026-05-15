## ADDED Requirements

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
