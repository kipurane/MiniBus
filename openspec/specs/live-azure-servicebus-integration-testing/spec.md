# live-azure-servicebus-integration-testing Specification

## Purpose
TBD - created by archiving change add-live-azure-service-bus-integration-tests. Update Purpose after archive.
## Requirements
### Requirement: Live Azure Service Bus tests require explicit opt-in
MiniBus SHALL provide live Azure Service Bus integration tests that run only when an explicit live-test opt-in and a non-empty Azure Service Bus connection string are both configured.

#### Scenario: Normal test run has no live configuration
- **WHEN** the test suite runs without the live-test opt-in or without the documented connection string
- **THEN** live Azure Service Bus tests are skipped with a clear non-sensitive reason
- **AND** infrastructure-free, emulator-backed, SQL, and Azure Storage test behavior remains unchanged

#### Scenario: Developer explicitly enables live tests
- **WHEN** `MINIBUS_RUN_LIVE_AZURE_SERVICEBUS_TESTS` is enabled and `MINIBUS_AZURE_SERVICEBUS_CONNECTION_STRING` is configured
- **THEN** the selected live Azure Service Bus tests attempt to run against that namespace

### Requirement: Live tests have a stable selection boundary
MiniBus SHALL place live Azure Service Bus coverage in a dedicated non-packable test project and SHALL assign a stable `LiveAzureServiceBus` category or equivalent test trait to every live test.

#### Scenario: Developer selects only live tests
- **WHEN** a developer runs the documented dedicated-project command with the live test category filter
- **THEN** only the live Azure Service Bus integration suite is selected

#### Scenario: Solution is built without live credentials
- **WHEN** the normal solution build runs without Azure credentials
- **THEN** the dedicated test project builds without contacting Azure Service Bus

### Requirement: Live tests isolate and clean up broker entities
MiniBus live tests SHALL create uniquely named temporary queues, topics, and subscriptions inside the configured namespace and SHALL attempt to remove every entity they create.

#### Scenario: Parallel scenarios create resources
- **WHEN** two live test scenarios execute concurrently
- **THEN** their entity names do not collide
- **AND** each scenario interacts only with its own resources

#### Scenario: Entity names approach Azure limits
- **WHEN** a generated queue, topic, or subscription name would exceed or violate the corresponding Azure Service Bus naming rules
- **THEN** the test name factory normalizes and shortens the name while retaining a collision-resistant token

#### Scenario: Scenario completes or fails
- **WHEN** a live scenario finishes after creating broker entities
- **THEN** its fixture performs best-effort cleanup in dependency-safe order
- **AND** a cleanup failure does not hide the primary test failure

### Requirement: Live transport dispatch is verified against the broker
MiniBus SHALL verify routed command sends and routed event publishes through production transport components against live Azure Service Bus.

#### Scenario: Routed command is sent to a live queue
- **WHEN** `AzureServiceBusTransportDispatcher` sends a command using an explicit route
- **THEN** the command is received from the routed live queue with the expected serialized body

#### Scenario: Routed event is published to a live subscription
- **WHEN** `AzureServiceBusTransportDispatcher` publishes an event using an explicit topic route
- **THEN** the event is received from the scenario's live topic subscription with the expected serialized body

### Requirement: Live messages preserve MiniBus metadata
MiniBus SHALL verify that production broker round-tripping preserves message identity, correlation, causation, message type, content type, and supported custom MiniBus headers.

#### Scenario: Command metadata round-trips through Azure Service Bus
- **WHEN** MiniBus sends a command containing message identity, correlation, causation, and custom header values
- **THEN** the received Service Bus message exposes the expected system properties and application properties
- **AND** the receive-side header mapper reconstructs the expected MiniBus header values

#### Scenario: Event metadata round-trips through a subscription
- **WHEN** MiniBus publishes an event containing MiniBus metadata
- **THEN** the subscribed message preserves that metadata without topic delivery removing or changing supported values

### Requirement: Scheduled delivery is verified with bounded timing
MiniBus SHALL verify scheduled message delivery against live Azure Service Bus using bounded observation and a documented timing tolerance.

#### Scenario: Routed message is scheduled
- **WHEN** MiniBus schedules a routed message for a near-future due time
- **THEN** the message is not observed as an ordinary available message before the accepted early-delivery tolerance
- **AND** it is received before the bounded scenario deadline with its body and metadata preserved

### Requirement: Live settlement and recoverability outcomes are verified
MiniBus SHALL verify representative completion, broker redelivery, retry, and dead-letter outcomes using live locked messages and production MiniBus processing behavior without deploying an Azure Functions host.

#### Scenario: Successful processing completes a live message
- **WHEN** `MiniBusProcessor` successfully handles a live received message through receiver-backed test settlement actions
- **THEN** the broker lock is completed
- **AND** the message is not available for redelivery

#### Scenario: Abandoned message is redelivered
- **WHEN** the live test harness abandons a locked message to create a broker redelivery
- **THEN** the message becomes receivable again with broker redelivery evidence
- **AND** MiniBus can process the redelivered message through its configured retry and completion path

#### Scenario: Immediate retry succeeds
- **WHEN** a test handler fails on its first invocation and succeeds within the configured immediate retry limit
- **THEN** MiniBus completes the live message without dead-lettering it

#### Scenario: Retries are exhausted
- **WHEN** live message processing continues to fail after configured retries are exhausted
- **THEN** MiniBus dead-letters the message
- **AND** the dead-letter subqueue exposes the expected MiniBus reason and non-secret diagnostic description

### Requirement: Live outbox replay uses deterministic broker message identity
MiniBus SHALL verify that dispatching a stored outbox operation uses its deterministic outgoing message ID as the Azure Service Bus `MessageId`.

#### Scenario: Stored operation is replayed
- **WHEN** the same `MiniBusOutboxStoredOperation` is dispatched more than once
- **THEN** every dispatch attempt uses the same broker `MessageId`
- **AND** a duplicate-detection-enabled test entity can recognize or suppress the replay

### Requirement: Live test waits are bounded and diagnostic
MiniBus live tests SHALL use cancellation-aware bounded polling or receive operations instead of fixed long sleeps, and SHALL report non-sensitive context when a deadline expires.

#### Scenario: Expected message arrives promptly
- **WHEN** the broker makes the expected message available before the scenario deadline
- **THEN** the test continues without waiting for the full timeout

#### Scenario: Expected broker state never appears
- **WHEN** a receive or state condition is not satisfied before its deadline
- **THEN** the test fails with the expected condition, safe entity alias, elapsed timeout, and last safe observation

### Requirement: Configured infrastructure failures remain visible
MiniBus live tests SHALL skip only for missing explicit opt-in or missing required configuration; failures after live execution is enabled MUST fail the test.

#### Scenario: Configured credentials are invalid
- **WHEN** live execution is enabled but authentication or authorization fails
- **THEN** the live test fails with a non-secret configuration or authorization diagnosis

#### Scenario: Configured namespace is unreachable
- **WHEN** live execution is enabled but network, DNS, entity-management, or broker operations fail
- **THEN** the live test fails rather than being converted to an infrastructure skip

### Requirement: Live test credentials remain protected
MiniBus live tests and automation SHALL NOT print the Azure Service Bus connection string, shared access key, or equivalent secret-bearing configuration.

#### Scenario: Live setup or broker operation fails
- **WHEN** a live test reports setup, authentication, timeout, assertion, or cleanup diagnostics
- **THEN** its output omits credentials and secret-bearing connection details

#### Scenario: CI executes live tests
- **WHEN** an optional CI workflow supplies Azure Service Bus credentials
- **THEN** the value is sourced from protected secret configuration
- **AND** the workflow does not dump the secret-bearing environment

### Requirement: Live integration execution is documented
MiniBus SHALL document live Azure Service Bus test prerequisites, permissions, configuration, selection, cost expectations, cleanup, troubleshooting, and CI boundaries.

#### Scenario: Developer prepares a live test namespace
- **WHEN** a developer reads the live integration documentation
- **THEN** it explains the dedicated-namespace recommendation, required data and management permissions, environment variables, and explicit test command

#### Scenario: Developer investigates stale resources
- **WHEN** a test run is interrupted before cleanup completes
- **THEN** the documentation explains how to identify prefixed test entities and remove them safely

#### Scenario: Pull request validation runs
- **WHEN** ordinary pull-request CI runs without live Azure secrets
- **THEN** live tests are not required
- **AND** an optional manual or secret-gated workflow can run them separately

