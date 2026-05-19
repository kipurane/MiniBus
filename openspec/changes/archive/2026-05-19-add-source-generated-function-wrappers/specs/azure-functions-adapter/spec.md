## ADDED Requirements

### Requirement: Azure Functions adapter supports optional generated wrappers
MiniBus Azure Functions integration SHALL support source-generated Service Bus trigger wrappers as an optional integration model that delegates to the existing `MiniBusProcessor` processing API.

#### Scenario: Generated wrapper delegates to adapter processor
- **WHEN** a generated Azure Functions Service Bus trigger wrapper receives a message
- **THEN** it delegates processing and settlement to `MiniBusProcessor.ProcessAsync(ServiceBusReceivedMessage, ServiceBusMessageActions, CancellationToken)`

#### Scenario: Manual wrapper remains supported
- **WHEN** an application uses a manually written Azure Functions Service Bus trigger wrapper
- **THEN** the wrapper remains a supported integration model and can delegate to the same `MiniBusProcessor` overloads

### Requirement: Azure Functions documentation covers generated and manual wrappers
MiniBus Azure Functions documentation SHALL describe both generated wrappers and manual wrappers, including when source generation is optional and when manual wrappers remain appropriate.

#### Scenario: Developer reads wrapper documentation
- **WHEN** a developer reads Azure Functions adapter documentation after generated wrappers are available
- **THEN** it shows how to declare generated wrappers and how to write the equivalent manual wrapper

#### Scenario: Developer chooses not to use generation
- **WHEN** a developer does not reference the source generator package
- **THEN** the documentation still provides the manual wrapper setup path
