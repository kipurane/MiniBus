## ADDED Requirements

### Requirement: Saga state follows durable processing outcome
MiniBus SHALL make saga state mutations requested during message processing durable only when the processing attempt's durable persistence boundary commits successfully.

#### Scenario: Saga handler succeeds with transactional persistence
- **WHEN** a saga handler mutates saga state during a processing attempt that uses transactional persistence
- **THEN** MiniBus commits the saga mutation as part of the same durable processing outcome as the incoming message state and outgoing operations

#### Scenario: Saga handler fails
- **WHEN** a saga handler throws during message processing
- **THEN** MiniBus does not make saga state mutations from that failed attempt durable

#### Scenario: Processing commit fails after saga handling
- **WHEN** saga handling succeeds but the processing persistence commit fails
- **THEN** MiniBus does not make saga state mutations from that attempt durable
- **AND** retry processing observes saga state from before the failed attempt

#### Scenario: Duplicate message is skipped
- **WHEN** persistence identifies the incoming logical message id as already processed before saga invocation
- **THEN** MiniBus does not invoke saga handlers for that duplicate delivery
- **AND** MiniBus does not mutate saga state for that duplicate delivery
