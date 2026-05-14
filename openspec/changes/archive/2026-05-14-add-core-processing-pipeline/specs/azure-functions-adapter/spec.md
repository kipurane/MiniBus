## ADDED Requirements

### Requirement: Azure Functions processor delegates to internal pipeline
The Azure Functions adapter SHALL keep the public `MiniBusProcessor` processing overloads while delegating processing orchestration to the internal MiniBus processing pipeline.

#### Scenario: Public processor overloads remain available
- **WHEN** an application references the Azure Functions adapter
- **THEN** `MiniBusProcessor.ProcessAsync(ServiceBusReceivedMessage, CancellationToken)`, `MiniBusProcessor.ProcessAsync(ServiceBusReceivedMessage, ServiceBusMessageActions, CancellationToken)`, and `MiniBusProcessor.ProcessAsync(ServiceBusReceivedMessage, IMiniBusMessageActions, CancellationToken)` remain available

#### Scenario: No-settlement overload uses pipeline
- **WHEN** the no-settlement overload processes a received Service Bus message
- **THEN** it uses the internal pipeline and preserves existing behavior for deserialization, handler invocation, saga invocation, direct dispatch, SQL inbox/outbox, and failure propagation

#### Scenario: Settlement overload uses pipeline
- **WHEN** the settlement-enabled overload processes a received Service Bus message
- **THEN** it uses the internal pipeline and preserves existing behavior for successful completion, immediate retries, delayed retry scheduling, dead-lettering, duplicate inbox completion, and persistence commit failures

### Requirement: Azure Functions settlement decisions are represented explicitly
The Azure Functions adapter SHALL represent settlement outcomes explicitly inside the pipeline before invoking Azure Service Bus settlement APIs.

#### Scenario: Processing succeeds
- **WHEN** settlement-enabled processing succeeds
- **THEN** the pipeline records a complete-message settlement decision and the adapter completes the received Service Bus message

#### Scenario: Delayed retry is selected
- **WHEN** recoverability selects a delayed retry
- **THEN** the pipeline records that a retry copy must be scheduled and the original received message completed

#### Scenario: Dead-letter is selected
- **WHEN** recoverability selects a dead-letter outcome
- **THEN** the pipeline records the dead-letter reason and description used by the adapter settlement action
