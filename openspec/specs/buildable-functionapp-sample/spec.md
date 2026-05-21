# buildable-functionapp-sample Specification

## Purpose
Defines the buildable Azure Functions sample application that demonstrates the stable MiniBus setup path with Service Bus transport, handler business logic, recoverability, and optional saga processing.
## Requirements
### Requirement: Function App sample is buildable
MiniBus SHALL provide a buildable Azure Functions isolated worker sample project under `samples/MiniBus.Samples.FunctionApp`.

#### Scenario: Sample project builds with the solution
- **WHEN** the solution is built or tested
- **THEN** the sample project compiles against the current MiniBus project references

#### Scenario: Sample project is included in the solution
- **WHEN** a developer opens `MiniBus.sln`
- **THEN** the Function App sample appears as a project rather than only loose solution items

### Requirement: Sample demonstrates host registration hook
The Function App sample SHALL show a complete Azure Functions isolated worker host path that registers MiniBus while keeping the Billing registration logic readable for a real host or reusable project template.

#### Scenario: Developer inspects sample startup
- **WHEN** a developer reads the sample startup code
- **THEN** it shows the Azure Functions host entry point and the coherent MiniBus service-registration hook used by the Billing reference workflow

### Requirement: Sample demonstrates MiniBus processing registration
The Function App sample SHALL demonstrate registration for MiniBus Azure Functions processing, message serialization, handlers, recoverability options, and saga services when saga code is included.

#### Scenario: Developer inspects MiniBus registration
- **WHEN** a developer reads the sample MiniBus registration code
- **THEN** it shows endpoint name, recoverability settings, serializer registration, handler registration, and any included saga registration

### Requirement: Sample demonstrates Azure Service Bus transport setup
The Function App sample SHALL demonstrate Azure Service Bus route configuration and dispatcher service registration needed by `MiniBusContext` outgoing operations.

#### Scenario: Developer inspects transport registration
- **WHEN** a developer reads the sample transport setup
- **THEN** it shows command, event, or scheduled-message routes and required transport dispatcher dependencies

### Requirement: Sample includes handler-facing business code
The Function App sample SHALL include at least one MiniBus handler that processes a command and uses `MiniBusContext` for outgoing work.

#### Scenario: Handler uses MiniBusContext
- **WHEN** a developer reads the sample handler
- **THEN** the handler receives a MiniBus message, `MiniBusContext`, and `CancellationToken`, and requests outgoing work without Azure SDK dependencies

### Requirement: Sample documents configuration and limits
The Function App sample SHALL document how to build and run the emulator-backed Billing reference workflow and clearly identify intentionally omitted production concerns.

#### Scenario: Developer reads sample documentation
- **WHEN** a developer opens the sample documentation
- **THEN** it explains build commands, emulator setup, local configuration, command submission, observable workflow steps, emulator limitations, and that live Azure Service Bus coverage and mandatory SQL persistence wiring remain outside this sample slice

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

