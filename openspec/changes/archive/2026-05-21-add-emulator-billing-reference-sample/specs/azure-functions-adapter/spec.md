## ADDED Requirements

### Requirement: Azure Functions sample shows local emulator execution path
The Azure Functions adapter documentation and samples SHALL include a runnable Billing reference path that processes Service Bus emulator messages through the isolated-worker Functions adapter.

#### Scenario: Emulator-backed sample uses Functions wrappers
- **WHEN** a developer runs the local Billing reference workflow
- **THEN** inbound emulator messages enter MiniBus through Azure Functions Service Bus trigger wrappers

#### Scenario: Runnable wrappers stay thin
- **WHEN** a developer reads the runnable Billing sample wrappers
- **THEN** each wrapper delegates trigger message processing and settlement directly to `MiniBusProcessor.ProcessAsync`
