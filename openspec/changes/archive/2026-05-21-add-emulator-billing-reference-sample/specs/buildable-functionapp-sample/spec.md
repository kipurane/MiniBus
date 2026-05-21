## ADDED Requirements

### Requirement: Billing sample provides an emulator-backed local workflow
The Function App sample SHALL provide a locally runnable Billing reference workflow against the Azure Service Bus emulator without requiring a real Azure Service Bus namespace.

#### Scenario: Repository owns local Billing topology
- **WHEN** a developer prepares the emulator-backed Billing sample workflow
- **THEN** the repository provides the local emulator configuration or assets needed for the Billing queues, event topic/subscription, and scheduled-message destination used by the sample

#### Scenario: Developer submits initial Billing command locally
- **WHEN** a developer follows the local sample workflow
- **THEN** the repository provides a documented way to submit the initial Billing command with the MiniBus message metadata required for Azure Functions processing

#### Scenario: Local workflow proves Billing processing path
- **WHEN** the emulator-backed sample workflow runs successfully
- **THEN** it demonstrates Billing command handling, outgoing receipt-command dispatch, `InvoiceCreated` publication, and Billing event subscription processing through MiniBus APIs

#### Scenario: Timeout behavior is described by validated local coverage
- **WHEN** a developer reads the emulator-backed Billing workflow guidance
- **THEN** it distinguishes validated local timeout scheduling or processing behavior from timeout behavior that remains outside the verified emulator workflow

## MODIFIED Requirements

### Requirement: Sample demonstrates host registration hook
The Function App sample SHALL show a complete Azure Functions isolated worker host path that registers MiniBus while keeping the Billing registration logic readable for a real host or reusable project template.

#### Scenario: Developer inspects sample startup
- **WHEN** a developer reads the sample startup code
- **THEN** it shows the Azure Functions host entry point and the coherent MiniBus service-registration hook used by the Billing reference workflow

### Requirement: Sample documents configuration and limits
The Function App sample SHALL document how to build and run the emulator-backed Billing reference workflow and clearly identify intentionally omitted production concerns.

#### Scenario: Developer reads sample documentation
- **WHEN** a developer opens the sample documentation
- **THEN** it explains build commands, emulator setup, local configuration, command submission, observable workflow steps, emulator limitations, and that live Azure Service Bus coverage and mandatory SQL persistence wiring remain outside this sample slice
