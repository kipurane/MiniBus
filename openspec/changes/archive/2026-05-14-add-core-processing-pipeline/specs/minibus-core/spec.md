## ADDED Requirements

### Requirement: Core processing seams remain transport independent
MiniBus core processing collaborators SHALL remain independent of Azure Functions and Azure Service Bus transport types when used by the internal processing pipeline.

#### Scenario: Handler invocation remains transport independent
- **WHEN** the processing pipeline invokes regular handlers
- **THEN** handler invocation uses the existing MiniBus core handler abstraction and does not expose Azure Functions or Azure Service Bus types to handlers

#### Scenario: Outgoing operation capture remains transport independent
- **WHEN** the processing pipeline captures outgoing operations for SQL outbox persistence
- **THEN** captured operations use MiniBus core persistence abstractions and do not depend on Azure Service Bus message types

#### Scenario: Recoverability decision model remains transport independent
- **WHEN** the processing pipeline evaluates recoverability after a failure
- **THEN** it uses the existing MiniBus core recoverability decision model without adding Azure Functions or Azure Service Bus dependencies to MiniBus.Core
