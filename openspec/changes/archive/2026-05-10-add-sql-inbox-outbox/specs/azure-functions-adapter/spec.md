## ADDED Requirements

### Requirement: Azure Functions processing supports SQL inbox checks
The Azure Functions adapter SHALL use the configured MiniBus inbox service during processing when SQL inbox/outbox persistence is enabled.

#### Scenario: Duplicate message is completed without handler invocation
- **WHEN** settlement-enabled processing receives a message already recorded in the inbox for the endpoint
- **THEN** the processor does not invoke handlers and completes the received Service Bus message

#### Scenario: Duplicate message without settlement does not invoke handlers
- **WHEN** no-settlement processing receives a message already recorded in the inbox for the endpoint
- **THEN** the processor does not invoke handlers and returns without calling settlement APIs

### Requirement: Azure Functions processing commits SQL outbox before completion
The Azure Functions adapter SHALL commit SQL inbox and outbox state before completing a received Service Bus message when SQL inbox/outbox persistence is enabled.

#### Scenario: SQL commit succeeds before completion
- **WHEN** settlement-enabled processing handles a message successfully with SQL persistence enabled
- **THEN** the processor commits inbox and outbox state before calling `CompleteMessageAsync`

#### Scenario: SQL commit failure prevents completion
- **WHEN** settlement-enabled processing handles a message but SQL inbox/outbox commit fails
- **THEN** the processor does not call `CompleteMessageAsync` and propagates the failure to the caller

### Requirement: Azure Functions processing captures outgoing operations when SQL outbox is enabled
The Azure Functions adapter SHALL capture outgoing operations requested through `MiniBusContext` during handler execution instead of directly dispatching them when SQL outbox persistence is enabled.

#### Scenario: Handler requests outgoing work
- **WHEN** a handler calls `Send`, `Publish`, or `Schedule` during SQL-backed processing
- **THEN** the processor captures the outgoing operation for SQL outbox persistence

#### Scenario: SQL outbox disabled preserves direct dispatch
- **WHEN** a handler calls `Send`, `Publish`, or `Schedule` and SQL outbox persistence is not enabled
- **THEN** the processor keeps the existing direct dispatch behavior
