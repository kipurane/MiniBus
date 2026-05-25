# buildable-functionapp-sample Specification

## Purpose
Defines the buildable Azure Functions sample application that demonstrates the stable MiniBus setup path with Service Bus transport, handler business logic, recoverability, and optional saga processing.
## Requirements
### Requirement: Function App sample is buildable
MiniBus SHALL provide a buildable Azure Functions isolated worker Billing sample project under `samples/MiniBus.Samples.Billing.FunctionApp`.

#### Scenario: Sample project builds with the solution
- **WHEN** the solution is built or tested
- **THEN** the Billing sample project compiles against the current MiniBus project references

#### Scenario: Sample project is included in the solution
- **WHEN** a developer opens `MiniBus.sln`
- **THEN** the Billing Function App sample appears as `MiniBus.Samples.Billing.FunctionApp` rather than only loose solution items

#### Scenario: Billing sample uses endpoint-specific identity
- **WHEN** a developer inspects the Billing sample directory, project file, assembly output, or root namespace
- **THEN** each uses the endpoint-specific `MiniBus.Samples.Billing.FunctionApp` identity instead of the generic `MiniBus.Samples.FunctionApp` identity

#### Scenario: Billing and Inventory sample names align
- **WHEN** a developer compares the sample endpoint projects
- **THEN** Billing uses `MiniBus.Samples.Billing.FunctionApp` and Inventory uses `MiniBus.Samples.Inventory.FunctionApp`

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

### Requirement: Billing sample provides a SQL-backed reliability reference path
The Function App sample SHALL provide an optional SQL-backed Billing reference path that composes the existing Azure Functions processing flow, Azure Service Bus routes, SQL inbox/outbox persistence, SQL saga persistence, and application-owned outbox dispatch without changing the handler-facing Billing APIs.

#### Scenario: Developer inspects SQL-backed Billing configuration
- **WHEN** a developer reads the SQL-backed Billing sample path
- **THEN** it shows explicit SQL schema setup, SQL persistence registration, and the application-owned outbox drain responsibility needed by the reliable workflow

#### Scenario: SQL-backed workflow captures durable Billing work
- **WHEN** the SQL-backed Billing workflow processes Billing messages that request outgoing receipt, event, and timeout work
- **THEN** it demonstrates SQL inbox participation, SQL outbox capture for outgoing work, and SQL-backed saga state for the Billing saga before outbox draining occurs

#### Scenario: SQL-backed workflow drains captured Billing work
- **WHEN** the SQL-backed Billing reference path drains pending outbox work through the existing SQL outbox dispatcher
- **THEN** it demonstrates the captured Billing send, publish, and scheduled timeout work flowing through the configured transport path

#### Scenario: Timer-triggered dispatcher host is documented
- **WHEN** a developer reads the SQL-backed Billing reference path
- **THEN** it presents a timer-triggered Azure Functions dispatcher as the preferred Functions-native automatic drain shape
- **AND** it explains why a separate dispatcher Function App is clearer for production-style ownership than hiding dispatch inside the message-processing Function App
- **AND** it notes that colocating the timer trigger in the existing Function App is acceptable for small deployments that intentionally choose one host boundary

#### Scenario: Timer-triggered dispatcher uses existing dispatch primitive
- **WHEN** the timer-triggered dispatcher reference path drains SQL outbox work
- **THEN** it resolves and invokes the existing `SqlMiniBusOutboxDispatcher`
- **AND** it does not duplicate SQL claim, dispatch, or failure-recording behavior in the Function App sample
