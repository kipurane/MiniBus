## 1. Project Setup

- [x] 1.1 Create `src/MiniBus.Testing` as a `net10.0` class library referencing only `MiniBus.Core`.
- [x] 1.2 Create `tests/MiniBus.Testing.Tests` with xUnit test dependencies and a project reference to `MiniBus.Testing`.
- [x] 1.3 Add both projects to `MiniBus.sln`.

## 2. Testable Context API

- [x] 2.1 Implement public captured operation models for sent, published, and scheduled outgoing operations.
- [x] 2.2 Implement `TestableMiniBusContext` deriving from `MiniBusContext`.
- [x] 2.3 Support deterministic default metadata and configurable endpoint name, message id, correlation id, causation id, and headers.
- [x] 2.4 Capture `Send`, `Publish`, and `Schedule` calls with original message object, message type, and schedule due time.
- [x] 2.5 Add dependency-free typed query helpers for sent, published, and scheduled operations, including single-result helpers if they stay small.

## 3. Tests And Documentation

- [x] 3.1 Add tests covering default metadata and configured metadata/header behavior.
- [x] 3.2 Add tests covering send, publish, and schedule capture.
- [x] 3.3 Add tests covering typed query helpers and single-result helper failure behavior.
- [x] 3.4 Add `MiniBus.Testing` README documentation with direct handler and saga handler testing examples.
- [x] 3.5 Confirm the testing package project file has no host, transport, storage, Azure SDK, SQL, observability, or test-framework dependencies.

## 4. Validation

- [x] 4.1 Run `dotnet test tests/MiniBus.Testing.Tests/MiniBus.Testing.Tests.csproj --no-restore`.
- [x] 4.2 Run the full test suite with `dotnet test --no-restore`.
- [x] 4.3 Run `openspec validate add-minibus-testing-package --strict`.
