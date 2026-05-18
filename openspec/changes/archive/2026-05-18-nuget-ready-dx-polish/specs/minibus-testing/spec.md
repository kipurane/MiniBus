## ADDED Requirements

### Requirement: Testing package documentation is included in the recommended developer workflow
MiniBus documentation SHALL present `MiniBus.Testing` as the recommended direct handler and saga handler testing package for application developers.

#### Scenario: Developer follows the golden path
- **WHEN** a developer follows MiniBus getting-started or golden-path documentation
- **THEN** it points to `MiniBus.Testing` for direct handler and saga handler unit tests

#### Scenario: Developer chooses a testing level
- **WHEN** a developer reads testing guidance
- **THEN** it distinguishes direct handler tests using `MiniBus.Testing` from processor, transport, SQL persistence, Azure Storage, and live integration tests

#### Scenario: Developer reads package documentation
- **WHEN** a developer reads the `MiniBus.Testing` package README
- **THEN** it shows creating `TestableMiniBusContext`, invoking a handler directly, and inspecting captured send, publish, or schedule operations
