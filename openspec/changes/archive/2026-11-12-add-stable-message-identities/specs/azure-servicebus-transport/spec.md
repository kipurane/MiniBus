## ADDED Requirements

### Requirement: Azure Service Bus envelopes use stable message identities
The Azure Service Bus transport SHALL use the configured MiniBus message identity registry when creating message type metadata and SHALL preserve the selected identity in Service Bus application and corresponding system properties.

#### Scenario: Registered contract is dispatched directly
- **WHEN** the transport sends, publishes, or schedules a contract with a registered stable identity
- **THEN** the Service Bus envelope carries that stable identity in MiniBus message type metadata

#### Scenario: Unregistered contract is dispatched directly
- **WHEN** the transport sends, publishes, or schedules a contract without a stable identity registration
- **THEN** the Service Bus envelope retains the existing assembly-qualified identity behavior

#### Scenario: Persisted operation is replayed
- **WHEN** the transport dispatches an outbox operation containing stored stable message type headers
- **THEN** it preserves the stored stable identity instead of replacing it with a newly derived CLR identity

#### Scenario: Delayed retry is scheduled
- **WHEN** the transport schedules a delayed retry copy of a received message
- **THEN** the retry copy preserves the received message identity unchanged
