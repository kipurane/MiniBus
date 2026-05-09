## ADDED Requirements

### Requirement: Azure Service Bus transport dispatches persisted outbox operations
The Azure Service Bus transport SHALL provide dispatch behavior that can be used by the SQL outbox dispatcher to send persisted outgoing operations.

#### Scenario: Persisted command operation is sent
- **WHEN** the SQL outbox dispatcher passes a persisted command send operation to the Azure Service Bus transport
- **THEN** the transport sends the command to its resolved queue destination

#### Scenario: Persisted event operation is published
- **WHEN** the SQL outbox dispatcher passes a persisted event publish operation to the Azure Service Bus transport
- **THEN** the transport publishes the event to its resolved topic destination

#### Scenario: Persisted scheduled operation is scheduled
- **WHEN** the SQL outbox dispatcher passes a persisted scheduled operation to the Azure Service Bus transport
- **THEN** the transport schedules the message for the stored due time

### Requirement: Persisted outbox dispatch preserves MiniBus headers
The Azure Service Bus transport SHALL preserve stored MiniBus headers when dispatching operations loaded from the SQL outbox.

#### Scenario: Stored headers become Service Bus properties
- **WHEN** a persisted outbox operation is dispatched through Azure Service Bus
- **THEN** the stored MiniBus headers are mapped to Service Bus application properties

#### Scenario: Stored correlation metadata is preserved
- **WHEN** a persisted outbox operation contains correlation and causation headers
- **THEN** the dispatched Service Bus message contains those values in the mapped metadata
