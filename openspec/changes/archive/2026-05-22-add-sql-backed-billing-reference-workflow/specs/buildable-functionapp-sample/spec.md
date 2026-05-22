## ADDED Requirements

### Requirement: Billing sample provides a SQL-backed reliability reference path
The Function App sample SHALL provide an optional SQL-backed Billing reference path that composes the existing Azure Functions processing flow, Azure Service Bus routes, SQL inbox/outbox persistence, and SQL saga persistence without changing the handler-facing Billing APIs.

#### Scenario: Developer inspects SQL-backed Billing configuration
- **WHEN** a developer reads the SQL-backed Billing sample path
- **THEN** it shows explicit SQL schema setup, SQL persistence registration, and the application-owned outbox drain responsibility needed by the reliable workflow

#### Scenario: SQL-backed workflow captures durable Billing work
- **WHEN** the SQL-backed Billing workflow processes Billing messages that request outgoing receipt, event, and timeout work
- **THEN** it demonstrates SQL inbox participation, SQL outbox capture for outgoing work, and SQL-backed saga state for the Billing saga before outbox draining occurs

#### Scenario: SQL-backed workflow drains captured Billing work
- **WHEN** the SQL-backed Billing reference path drains pending outbox work through the existing SQL outbox dispatcher
- **THEN** it demonstrates the captured Billing send, publish, and scheduled timeout work flowing through the configured transport path

## MODIFIED Requirements

### Requirement: Sample documents configuration and limits
The Function App sample SHALL document how to build and run the emulator-backed Billing reference workflow and its optional SQL-backed reliability path while clearly identifying intentionally omitted production concerns.

#### Scenario: Developer reads sample documentation
- **WHEN** a developer opens the sample documentation
- **THEN** it explains build commands, emulator setup, local configuration, command submission, SQL schema setup, SQL persistence registration, explicit outbox draining, observable workflow steps, local infrastructure limits, and that live Azure Service Bus coverage remains outside this sample slice
