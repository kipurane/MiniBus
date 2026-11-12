## ADDED Requirements

### Requirement: Azure Functions processing resolves stable message identities
The Azure Functions adapter SHALL resolve received MiniBus message type metadata through the configured identity registry before deserialization and SHALL retain the existing CLR identity fallback.

#### Scenario: Registered stable identity is processed
- **WHEN** an Azure Functions-triggered message contains a registered stable identity
- **THEN** MiniBus deserializes and invokes handlers using the CLR contract type mapped by the registry

#### Scenario: Registered legacy alias is processed
- **WHEN** an Azure Functions-triggered message contains an alias for the current CLR contract
- **THEN** MiniBus processes the message using the current CLR contract type

#### Scenario: Identity cannot be resolved
- **WHEN** received type metadata matches no registered identity or alias and cannot be resolved through the CLR fallback
- **THEN** MiniBus raises a clear resolution failure that flows through existing recoverability and settlement behavior

### Requirement: Azure Functions registration shares one identity registry
MiniBus Azure Functions registration SHALL make the configured message identity registry available consistently to processing, transport message creation, claim-check behavior, and configured persistence components.

#### Scenario: Identities are configured through processor setup
- **WHEN** an application registers stable identities during MiniBus Azure Functions configuration
- **THEN** all MiniBus components resolved from that service provider use the same mappings

#### Scenario: No identities are configured
- **WHEN** an application uses the existing Azure Functions registration without stable identity mappings
- **THEN** registration succeeds and existing CLR identity behavior remains available
