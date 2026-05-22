## MODIFIED Requirements

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
