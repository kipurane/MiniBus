## MODIFIED Requirements

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
