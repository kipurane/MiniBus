## MODIFIED Requirements

### Requirement: Tier 1 reference solution smoke test
MiniBus SHALL provide always-on high-level acceptance coverage that verifies sample-style MiniBus reference endpoints can be assembled through dependency injection and process the representative Billing workflow without requiring Docker, live Azure Service Bus, or a real Azure Functions host.

#### Scenario: Sample-style billing workflow composes
- **WHEN** the Tier 1 acceptance test builds a real service provider using sample-style MiniBus registration and processes a `CreateInvoice` Service Bus message through `MiniBusProcessor`
- **THEN** MiniBus invokes the Billing handler, publishes the invoice-created event, sends the invoice-receipt command, sends the Inventory reservation command, schedules the saga timeout message, and completes the received message through recording settlement actions

#### Scenario: Tier 1 test is infrastructure-free
- **WHEN** the normal test suite runs without Docker, Azure Service Bus, or an Azure Functions host
- **THEN** the Tier 1 acceptance coverage uses recording or fake transport and settlement dependencies and remains eligible to run with the normal unit and component tests

## ADDED Requirements

### Requirement: Emulator-backed reference acceptance covers endpoint boundary
MiniBus SHALL provide emulator-backed high-level acceptance coverage that verifies the Billing reference workflow dispatches the Inventory command across the Azure Service Bus boundary and that the Inventory sample endpoint processes it when the local emulator infrastructure is available.

#### Scenario: Emulator workflow reaches Inventory endpoint
- **WHEN** the emulator-backed acceptance workflow seeds Billing and runs the sample-style Billing and Inventory processors against the repo-owned emulator topology
- **THEN** Billing dispatches `ReserveInventory` to the Inventory queue and Inventory handles that command through its own endpoint registration path

#### Scenario: Emulator workflow follows existing local dependency behavior
- **WHEN** the Service Bus emulator is not reachable through the documented local connection path
- **THEN** the emulator-backed endpoint-boundary acceptance coverage is skipped without failing the normal infrastructure-free test run
