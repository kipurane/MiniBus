# reference-solution-acceptance-tests Specification

## Purpose
Defines high-level reference solution acceptance tests that prove MiniBus can be assembled through sample-style/public registration and process realistic workflows across core, Azure Functions processing, Azure Service Bus transport, saga, and SQL persistence pieces.
## Requirements
### Requirement: Tier 1 reference solution smoke test
MiniBus SHALL provide always-on high-level acceptance coverage that verifies sample-style MiniBus reference endpoints can be assembled through dependency injection and process the representative Billing workflow without requiring Docker, live Azure Service Bus, or a real Azure Functions host.

#### Scenario: Sample-style billing workflow composes
- **WHEN** the Tier 1 acceptance test builds a real service provider using sample-style MiniBus registration and processes a `CreateInvoice` Service Bus message through `MiniBusProcessor`
- **THEN** MiniBus invokes the Billing handler, publishes the invoice-created event, sends the invoice-receipt command, sends the Inventory reservation command, schedules the saga timeout message, and completes the received message through recording settlement actions

#### Scenario: Tier 1 test is infrastructure-free
- **WHEN** the normal test suite runs without Docker, Azure Service Bus, or an Azure Functions host
- **THEN** the Tier 1 acceptance coverage uses recording or fake transport and settlement dependencies and remains eligible to run with the normal unit and component tests

### Requirement: Emulator-backed reference acceptance covers endpoint boundary
MiniBus SHALL provide emulator-backed high-level acceptance coverage that verifies the Billing reference workflow dispatches the Inventory command across the Azure Service Bus boundary and that the Inventory sample endpoint processes it when the local emulator infrastructure is available.

#### Scenario: Emulator workflow reaches Inventory endpoint
- **WHEN** the emulator-backed acceptance workflow seeds Billing and runs the sample-style Billing and Inventory processors against the repo-owned emulator topology
- **THEN** Billing dispatches `ReserveInventory` to the Inventory queue and Inventory handles that command through its own endpoint registration path

#### Scenario: Emulator workflow follows existing local dependency behavior
- **WHEN** the Service Bus emulator is not reachable through the documented local connection path
- **THEN** the emulator-backed endpoint-boundary acceptance coverage is skipped without failing the normal infrastructure-free test run

### Requirement: Tier 2 SQL-backed reference scenario
MiniBus SHALL provide SQL-backed high-level acceptance scenarios that verify the Billing reference workflow composes with sample-style SQL persistence enabled, including SQL inbox duplicate handling, SQL saga persistence, durable outbox capture, and later dispatch of persisted outbox work.

#### Scenario: SQL-backed workflow records durable processing effects
- **WHEN** the Tier 2 acceptance scenario runs with SQL Server available through Testcontainers or a documented external SQL Server/Azure SQL test connection string and processes the Billing reference workflow with SQL persistence enabled
- **THEN** MiniBus records processed Billing messages in the SQL inbox, captures outgoing send, publish, or schedule work in the SQL outbox as part of successful processing transactions, and persists Billing saga state through SQL

#### Scenario: SQL-backed workflow skips a duplicate Billing message
- **WHEN** the Tier 2 acceptance scenario processes a duplicate Billing message after the SQL inbox has recorded the successful first delivery
- **THEN** MiniBus completes the duplicate without re-running Billing handler or saga work and without capturing duplicate outbox work

#### Scenario: SQL-backed workflow drains captured outbox work
- **WHEN** the Tier 2 acceptance scenario processes the Billing reference workflow with SQL persistence enabled and then runs `SqlMiniBusOutboxDispatcher.DispatchPendingAsync`
- **THEN** MiniBus dispatches the persisted Billing receipt command, invoice-created event, and invoice-payment-timeout schedule through the configured transport abstraction
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
