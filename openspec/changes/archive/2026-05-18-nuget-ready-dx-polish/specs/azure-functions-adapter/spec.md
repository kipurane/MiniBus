## ADDED Requirements

### Requirement: Azure Functions documentation identifies manual wrappers as current support
MiniBus Azure Functions documentation SHALL describe manual Service Bus trigger wrappers as the current supported Azure Functions integration model and SHALL identify source-generated wrappers as future work.

#### Scenario: Developer reads Azure Functions adapter documentation
- **WHEN** a developer reads the Azure Functions adapter documentation
- **THEN** it shows a thin manual Azure Function wrapper using `ServiceBusTrigger` and delegating to `MiniBusProcessor.ProcessAsync`

#### Scenario: Developer looks for generated wrappers
- **WHEN** a developer reads the Azure Functions adapter documentation before source generation exists
- **THEN** it states that source-generated wrappers are deferred and manual wrappers remain supported

#### Scenario: Developer configures the adapter
- **WHEN** a developer follows the Azure Functions adapter documentation
- **THEN** it shows adapter registration, endpoint options, recoverability options, and the related transport/persistence registrations needed by the documented setup path
