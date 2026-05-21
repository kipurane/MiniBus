## MODIFIED Requirements

### Requirement: Root documentation provides a golden path
MiniBus SHALL provide a concise root documentation path for starting an early Azure Functions application with the project template or manual setup guidance for Azure Service Bus, SQL persistence, recoverability, and handler testing.

#### Scenario: Developer starts from root README
- **WHEN** a developer reads the root README
- **THEN** it points at the project-template entry path and still directs them through the current recommended setup flow for core contracts, handler registration, manual Azure Functions wrappers, Azure Service Bus routes, SQL persistence scripts, recoverability, outbox dispatch, observability, and testing

#### Scenario: Deferred tooling is described
- **WHEN** a developer reads the setup documentation
- **THEN** it clearly states that source-generated wrappers and Roslyn analyzers are optional tooling and that live Azure integration tests and automatic infrastructure provisioning remain future work
