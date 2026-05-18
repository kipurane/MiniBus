# reference-solution-acceptance-tests Specification

## Purpose
Defines high-level reference solution acceptance tests that prove MiniBus can be assembled through sample-style/public registration and process realistic workflows across core, Azure Functions processing, Azure Service Bus transport, saga, and SQL persistence pieces.
## Requirements
### Requirement: Tier 1 reference solution smoke test
MiniBus SHALL provide an always-on high-level acceptance test that verifies a sample-style MiniBus solution can be assembled through dependency injection and process a representative billing command without requiring Docker, live Azure Service Bus, or a real Azure Functions host.

#### Scenario: Sample-style billing workflow composes
- **WHEN** the Tier 1 acceptance test builds a real service provider using sample-style MiniBus registration and processes a `CreateInvoice` Service Bus message through `MiniBusProcessor`
- **THEN** MiniBus invokes the billing handler, publishes the invoice-created event, sends the invoice-receipt command, schedules the saga timeout message, and completes the received message through recording settlement actions

#### Scenario: Tier 1 test is infrastructure-free
- **WHEN** the normal test suite runs without Docker, Azure Service Bus, or an Azure Functions host
- **THEN** the Tier 1 acceptance test uses recording or fake transport and settlement dependencies and remains eligible to run with the normal unit and component tests

### Requirement: Tier 2 SQL-backed reference scenario
MiniBus SHALL provide SQL-backed high-level acceptance scenarios that verify the reference workflow composes with SQL persistence enabled, including durable capture and later dispatch of persisted outbox work.

#### Scenario: SQL-backed workflow records durable processing effects
- **WHEN** the Tier 2 acceptance scenario runs with SQL Server available through Testcontainers or a documented external SQL Server/Azure SQL test connection string and processes the reference billing message with SQL persistence enabled
- **THEN** MiniBus records the incoming message in the SQL inbox and captures outgoing send, publish, or schedule work in the SQL outbox as part of the successful processing transaction

#### Scenario: SQL-backed workflow drains captured outbox work
- **WHEN** the Tier 2 acceptance scenario processes the reference billing workflow with SQL persistence enabled and then runs `SqlMiniBusOutboxDispatcher.DispatchPendingAsync`
- **THEN** MiniBus dispatches the persisted billing receipt command, invoice-created event, and invoice-payment-timeout schedule through the configured transport abstraction
- **AND** MiniBus marks the dispatched SQL outbox rows as dispatched so they no longer appear as pending or reclaimable work

#### Scenario: SQL-backed scenario follows existing environment behavior
- **WHEN** the Tier 2 acceptance scenario runs without Docker availability and without the documented SQL Server/Azure SQL test connection string
- **THEN** the SQL-backed acceptance scenario is skipped with a clear reason without failing the normal infrastructure-free test run

### Requirement: Reference acceptance tests remain focused
MiniBus reference solution acceptance tests SHALL stay focused on cross-package composition outcomes and SHALL NOT replace lower-level unit, adapter, transport, SQL, or Azure Storage integration coverage.

#### Scenario: Tests avoid broad behavior retesting
- **WHEN** acceptance test assertions are added for the reference workflows
- **THEN** they verify high-level composition outcomes such as resolved services, handler execution, outgoing operation capture or dispatch, saga timeout behavior, persistence effects, and settlement rather than reasserting every low-level transport or SQL persistence detail

#### Scenario: Tests provide observability anchors without instrumentation
- **WHEN** future observability work needs representative processing workflows for tracing, logging, or metrics acceptance criteria
- **THEN** the reference solution acceptance tests provide stable workflow scenarios without requiring this change to add observability behavior
