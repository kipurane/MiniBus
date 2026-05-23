## MODIFIED Requirements

### Requirement: Sample documents configuration and limits
The Function App sample SHALL document how to build and run the emulator-backed Billing and Inventory reference workflow and the optional SQL-backed Billing reliability path while clearly identifying intentionally omitted production concerns.

#### Scenario: Developer reads sample documentation
- **WHEN** a developer opens the sample documentation
- **THEN** it explains build commands, emulator setup, local configuration, command submission, Billing and Inventory host startup, SQL schema setup, Billing SQL persistence registration, explicit outbox draining, observable workflow steps, local infrastructure limits, and that live Azure Service Bus coverage remains outside this sample slice

### Requirement: Billing sample provides an emulator-backed local workflow
The Function App sample SHALL provide a locally runnable Billing reference workflow against the Azure Service Bus emulator without requiring a real Azure Service Bus namespace.

#### Scenario: Repository owns local Billing topology
- **WHEN** a developer prepares the emulator-backed Billing sample workflow
- **THEN** the repository provides the local emulator configuration or assets needed for the Billing queues, event topic/subscription, scheduled-message destination, and Inventory queue used by the sample routes

#### Scenario: Developer submits initial Billing command locally
- **WHEN** a developer follows the local sample workflow
- **THEN** the repository provides a documented way to submit the initial Billing command with the MiniBus message metadata required for Azure Functions processing

#### Scenario: Local workflow proves Billing processing path
- **WHEN** the emulator-backed sample workflow runs successfully
- **THEN** it demonstrates Billing command handling, outgoing receipt-command dispatch, `InvoiceCreated` publication, Billing event subscription processing, and outgoing Inventory command dispatch through MiniBus APIs

#### Scenario: Timeout behavior is described by validated local coverage
- **WHEN** a developer reads the emulator-backed Billing workflow guidance
- **THEN** it distinguishes validated local timeout scheduling or processing behavior from timeout behavior that remains outside the verified emulator workflow

## ADDED Requirements

### Requirement: Inventory sample extends the emulator-backed reference workflow
MiniBus SHALL provide a sibling Azure Functions Inventory sample endpoint that consumes the Billing workflow's Inventory command through the Azure Service Bus emulator without sharing the Billing endpoint identity.

#### Scenario: Inventory endpoint owns its input queue
- **WHEN** a developer inspects the Inventory sample endpoint
- **THEN** it shows an Inventory endpoint name, Inventory queue trigger wrapper, Inventory handler registration, and handler-facing `ReserveInventory` processing separate from the Billing Function App

#### Scenario: Shared command crosses sample project boundary
- **WHEN** Billing sends the Inventory-owned command from the reference workflow
- **THEN** Billing and Inventory use an explicit shared sample contract boundary and Billing routes `ReserveInventory` to the Inventory queue

#### Scenario: Local workflow proves cross-endpoint processing
- **WHEN** the emulator-backed Billing and Inventory reference workflow runs successfully
- **THEN** the seeded Billing command causes Inventory to process the dispatched `ReserveInventory` command through its own MiniBus endpoint path
