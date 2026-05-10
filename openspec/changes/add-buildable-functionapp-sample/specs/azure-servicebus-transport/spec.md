## ADDED Requirements

### Requirement: Azure Service Bus sample shows transport registration
The Azure Service Bus transport documentation and samples SHALL include buildable sample code showing route and dispatcher registration required for outgoing operations.

#### Scenario: Sample configures routes
- **WHEN** a developer reads the buildable Function App sample
- **THEN** it shows route configuration for commands, events, or scheduled messages used by the sample handlers

#### Scenario: Sample registers dispatch services
- **WHEN** a developer reads the buildable Function App sample
- **THEN** it shows the transport services required by `MiniBusContext.Send`, `Publish`, and `Schedule`
