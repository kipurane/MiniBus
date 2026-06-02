# basic-saga-support Specification

## Purpose
Defines MiniBus saga support for long-running workflows with explicit correlation, transport-independent persistence abstractions, completed-saga behavior, and Azure Functions integration through core processing seams.

## Requirements
### Requirement: Saga contracts define long-running workflow state
MiniBus SHALL provide transport-independent saga contracts for saga classes and saga data so applications can model long-running workflows without depending on Azure Functions, Azure Service Bus, or SQL APIs.

#### Scenario: Application defines saga data
- **WHEN** an application references MiniBus saga contracts
- **THEN** it can define saga data with an id, correlation id, and completion state

#### Scenario: Application defines a saga
- **WHEN** an application creates a saga type
- **THEN** the saga can handle MiniBus messages while using attached saga data

### Requirement: Saga correlation is explicit
MiniBus SHALL require explicit correlation configuration for starting and continuing messages.

#### Scenario: Starting message correlation is configured
- **WHEN** a saga maps a starting message to a saga correlation value
- **THEN** MiniBus can create new saga data using that correlation value

#### Scenario: Continuing message correlation is configured
- **WHEN** a saga maps a continuing message to a saga correlation value
- **THEN** MiniBus can load matching saga data for that message

#### Scenario: Correlation mapping is missing
- **WHEN** a saga message has no configured correlation mapping
- **THEN** MiniBus fails with a clear configuration error

### Requirement: Saga finder supports custom correlation logic
MiniBus SHALL support custom saga finder behavior for message types that cannot use simple property-to-property correlation.

#### Scenario: Custom finder returns a correlation value
- **WHEN** a custom finder is registered for a saga message type
- **THEN** MiniBus uses the finder result to load or create saga data

### Requirement: Saga persistence is abstracted from storage providers
MiniBus SHALL define a transport-independent saga persistence abstraction for loading, creating, saving, and completing saga data.

#### Scenario: Existing saga data is loaded
- **WHEN** a correlated message arrives for an existing saga
- **THEN** MiniBus loads the saga data before invoking the saga handler

#### Scenario: New saga data is created
- **WHEN** a configured starting message arrives and no matching saga data exists
- **THEN** MiniBus creates new saga data before invoking the saga handler

#### Scenario: Saga data is saved after success
- **WHEN** saga handling completes successfully
- **THEN** MiniBus saves the saga data

#### Scenario: Saga data is not saved after failure
- **WHEN** saga handling throws an exception
- **THEN** MiniBus does not save saga data for that failed attempt

### Requirement: Saga persistence supports optimistic concurrency metadata
MiniBus SHALL include optimistic-concurrency-ready metadata in saga persistence operations so storage providers can detect concurrent updates.

#### Scenario: Saga state is saved with version metadata
- **WHEN** MiniBus saves existing saga data
- **THEN** the save operation includes the version metadata returned by the load operation

### Requirement: Completed sagas are terminal
MiniBus SHALL support marking saga state as completed and MUST NOT invoke saga handlers for completed saga state.

#### Scenario: Saga marks itself complete
- **WHEN** a saga handler marks saga state complete during successful handling
- **THEN** MiniBus persists the saga as completed

#### Scenario: Message correlates to completed saga
- **WHEN** a message correlates to saga state that is already completed
- **THEN** MiniBus does not invoke the saga handler for that message

### Requirement: Saga invocation preserves MiniBus context
MiniBus SHALL invoke saga handlers with the same transport-independent MiniBus context semantics as regular handlers.

#### Scenario: Saga handler reads context metadata
- **WHEN** a saga handler is invoked
- **THEN** it can read endpoint name, message id, correlation id, causation id, and headers from `MiniBusContext`

#### Scenario: Saga handler dispatches outgoing operations
- **WHEN** a saga handler sends, publishes, or schedules messages through `MiniBusContext`
- **THEN** outgoing operations preserve MiniBus correlation and causation metadata

### Requirement: Azure Functions adapter remains a thin host adapter for sagas
MiniBus Azure Functions processing SHALL invoke saga behavior through core abstractions without exposing Azure Functions or Azure Service Bus trigger types to saga handlers.

#### Scenario: Saga is processed from a Service Bus trigger message
- **WHEN** `MiniBusProcessor` processes a Service Bus trigger message for a saga
- **THEN** the saga handler receives only the MiniBus message, saga data, `MiniBusContext`, and `CancellationToken`

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

### Requirement: Saga behavior is documented and tested
MiniBus SHALL document the minimal saga model and include tests for starting, loading, completing, and preserving context during saga handling.

#### Scenario: Documentation shows a sample saga
- **WHEN** a developer reads the saga documentation or sample
- **THEN** it shows saga data, explicit correlation, message handling, and completion

#### Scenario: Unit tests cover basic saga lifecycle
- **WHEN** the test suite runs
- **THEN** it verifies saga start behavior, existing saga loading, completed saga behavior, and context preservation

