## ADDED Requirements

### Requirement: Azure Functions sample shows complete adapter registration
The Azure Functions adapter documentation and samples SHALL include a buildable isolated worker sample showing complete adapter registration.

#### Scenario: Sample registers adapter services
- **WHEN** a developer reads the buildable Function App sample
- **THEN** it shows `AddMiniBusAzureFunctions` registration with endpoint and recoverability options

#### Scenario: Sample keeps trigger wrapper thin
- **WHEN** a developer reads the sample Service Bus trigger function
- **THEN** the function delegates directly to `MiniBusProcessor.ProcessAsync`
