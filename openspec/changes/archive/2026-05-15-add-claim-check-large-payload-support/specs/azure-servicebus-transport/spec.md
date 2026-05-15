## ADDED Requirements

### Requirement: Azure Service Bus transport sends claim-checked large payloads
The Azure Service Bus transport SHALL create Service Bus messages that use MiniBus claim-check bodies and headers when outgoing serialized payloads exceed the configured claim-check threshold.

#### Scenario: Large command send is claim-checked
- **WHEN** MiniBus sends a command whose serialized body exceeds the configured claim-check threshold
- **THEN** the Azure Service Bus transport sends one Service Bus message to the routed queue with a compact claim-check body and MiniBus claim-check application properties

#### Scenario: Large event publish is claim-checked
- **WHEN** MiniBus publishes an event whose serialized body exceeds the configured claim-check threshold
- **THEN** the Azure Service Bus transport sends one Service Bus message to the routed topic with a compact claim-check body and MiniBus claim-check application properties

#### Scenario: Large scheduled message is claim-checked
- **WHEN** MiniBus schedules a message whose serialized body exceeds the configured claim-check threshold
- **THEN** the Azure Service Bus transport schedules one Service Bus message for the requested due time with a compact claim-check body and MiniBus claim-check application properties

### Requirement: Azure Service Bus transport preserves claim-check metadata
The Azure Service Bus transport SHALL preserve claim-check metadata alongside existing MiniBus headers and Service Bus system properties.

#### Scenario: Claim-check headers become application properties
- **WHEN** a claim-checked Service Bus message is created
- **THEN** the MiniBus claim-check headers are mapped to Service Bus application properties as string values

#### Scenario: Service Bus system properties remain mapped
- **WHEN** a claim-checked Service Bus message is created with message id, correlation id, content type, and message type headers
- **THEN** the Service Bus message system properties mirror those values where the Azure SDK exposes matching properties

### Requirement: Azure Service Bus transport dispatches persisted claim-check operations
The Azure Service Bus transport SHALL dispatch SQL outbox operations containing claim-check bodies and headers without reserializing or losing claim-check metadata.

#### Scenario: Persisted claim-check send is dispatched
- **WHEN** the SQL outbox dispatcher passes a persisted claim-checked command send operation to the Azure Service Bus transport
- **THEN** the transport sends the stored compact body and stored MiniBus claim-check headers to the routed queue

#### Scenario: Persisted claim-check publish is dispatched
- **WHEN** the SQL outbox dispatcher passes a persisted claim-checked event publish operation to the Azure Service Bus transport
- **THEN** the transport sends the stored compact body and stored MiniBus claim-check headers to the routed topic

#### Scenario: Persisted claim-check schedule is dispatched
- **WHEN** the SQL outbox dispatcher passes a persisted claim-checked scheduled operation to the Azure Service Bus transport
- **THEN** the transport schedules the stored compact body and stored MiniBus claim-check headers for the persisted due time
