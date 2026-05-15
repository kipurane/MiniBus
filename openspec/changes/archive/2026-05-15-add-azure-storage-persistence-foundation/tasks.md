## 1. Package and Registration

- [x] 1.1 Create `src/MiniBus.Persistence.AzureStorage` and add it to `MiniBus.sln`.
- [x] 1.2 Add package references for Azure Blob Storage and dependency injection abstractions while keeping Azure SDK dependencies isolated to the Azure Storage package.
- [x] 1.3 Add Azure Storage persistence options for connection configuration, container name, blob prefix, and optional retention/expiry metadata.
- [x] 1.4 Add dependency injection registration APIs for connection-string registration and caller-provided Blob client/container factory registration.
- [x] 1.5 Validate required configuration and produce actionable errors for missing or invalid Blob payload store settings.

## 2. Payload Store Contracts

- [x] 2.1 Define a MiniBus-owned payload reference type that contains container name, blob name, payload length, content type, created timestamp, and optional expiry timestamp without exposing Azure SDK types.
- [x] 2.2 Define a payload store contract for writing, reading, and deleting payload bytes by payload reference.
- [x] 2.3 Add clear exception or failure types for payload-not-found and invalid payload reference scenarios.
- [x] 2.4 Decide and implement streaming and/or `BinaryData` convenience APIs according to the design.

## 3. Blob Payload Store Implementation

- [x] 3.1 Implement collision-resistant blob naming using the configured prefix and generated payload ids.
- [x] 3.2 Support deterministic payload identifiers for tests and explicit callers, including validation for unsafe identifiers.
- [x] 3.3 Implement payload writes with content type, length, created timestamp, optional expiry metadata, and MiniBus payload metadata.
- [x] 3.4 Implement payload reads that return the exact stored bytes and reject missing or incompatible references clearly.
- [x] 3.5 Implement idempotent payload delete behavior for existing and already-absent blobs.

## 4. Tests

- [x] 4.1 Create `tests/MiniBus.Persistence.AzureStorage.Tests` and add it to `MiniBus.sln`.
- [x] 4.2 Add unit tests for option validation, registration behavior, payload reference shape, and blob name validation without requiring Azure Storage infrastructure.
- [x] 4.3 Add Testcontainers-backed Azurite integration test setup for Blob Storage-compatible payload store tests, with an optional live Azure Storage connection-string fallback.
- [x] 4.4 Add integration tests for payload write/read/delete behavior, metadata preservation, deterministic ids, missing payloads, and idempotent deletes.
- [x] 4.5 Ensure integration tests skip with a clear reason when Docker/Testcontainers and a live storage connection string are unavailable.

## 5. Documentation and Backlog

- [x] 5.1 Document Azure Storage payload store registration, configuration, and current scope versus future claim-check processing.
- [x] 5.2 Update `openspec/project.md` Remaining feature backlog to mark the Azure Storage package and Blob payload store foundation according to the implemented scope.
- [x] 5.3 Run the relevant unit tests and any available Azure Storage integration tests.
