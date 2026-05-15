## ADDED Requirements

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
