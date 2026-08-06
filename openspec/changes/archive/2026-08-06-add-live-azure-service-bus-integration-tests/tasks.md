## 1. Dedicated Live Test Project

- [x] 1.1 Create the non-packable `tests/MiniBus.AzureServiceBus.IntegrationTests` project with references to the MiniBus runtime packages needed for transport, processing, recoverability, and stored outbox operation scenarios.
- [x] 1.2 Add the integration test project to `MiniBus.sln` and verify that building the solution does not contact Azure Service Bus.
- [x] 1.3 Add shared live-test settings for `MINIBUS_RUN_LIVE_AZURE_SERVICEBUS_TESTS` and `MINIBUS_AZURE_SERVICEBUS_CONNECTION_STRING`.
- [x] 1.4 Add a live test fact/guard that assigns `Category=LiveAzureServiceBus`, skips only when opt-in or configuration is missing, and never includes secret-bearing values in its reason.
- [x] 1.5 Add infrastructure-free tests for opt-in parsing, missing-configuration behavior, and secret-safe diagnostics.

## 2. Broker Test Infrastructure

- [x] 2.1 Implement a centralized queue/topic/subscription name factory with the `minibus-live` prefix, run/scenario tokens, character normalization, per-entity length handling, and collision-resistant truncation.
- [x] 2.2 Add infrastructure-free tests for valid names, long scenario names, unsupported characters, entity-specific limits, and uniqueness.
- [x] 2.3 Implement an asynchronous live namespace fixture using `ServiceBusAdministrationClient` and `ServiceBusClient` that records successfully created entities and performs reverse-order best-effort cleanup.
- [x] 2.4 Add fixture helpers for ordinary queues, duplicate-detection queues, topics, subscriptions, normal receivers, and dead-letter receivers.
- [x] 2.5 Implement cancellation-aware bounded receive and condition-polling helpers that fail with safe entity aliases, deadlines, and non-secret last observations.
- [x] 2.6 Add infrastructure-free tests for bounded polling success, timeout diagnostics, cancellation, and connection-string redaction.
- [x] 2.7 Add receiver-backed test settlement actions for completing and dead-lettering messages through `MiniBusProcessor`, plus explicit test-harness support for broker abandon/redelivery.

## 3. Live Transport And Metadata Coverage

- [x] 3.1 Add a live routed-command scenario that dispatches through `AzureServiceBusTransportDispatcher` and verifies the received serialized body.
- [x] 3.2 Extend the routed-command scenario to verify message ID, correlation ID, causation ID, message type, content type, custom application properties, and receive-side MiniBus header mapping.
- [x] 3.3 Add a live routed-event scenario that publishes through the production dispatcher and verifies body and metadata round-tripping through a topic subscription.
- [x] 3.4 Verify the transport scenarios use isolated entities, bounded receives, and category-based live selection.

## 4. Live Scheduling Coverage

- [x] 4.1 Add a live scheduled-message scenario using the production MiniBus dispatcher and a near-future due time.
- [x] 4.2 Verify scheduled delivery is not observed before the accepted early-delivery tolerance and is received within the bounded scenario deadline.
- [x] 4.3 Verify the scheduled message preserves its serialized body and MiniBus metadata after broker delivery.

## 5. Live Settlement And Recoverability Coverage

- [x] 5.1 Add a live processing scenario that runs `MiniBusProcessor` with receiver-backed settlement actions and verifies successful processing completes the broker message.
- [x] 5.2 Add an abandon/redelivery scenario that verifies broker redelivery evidence and then processes the redelivered message through MiniBus.
- [x] 5.3 Add a handler scenario that fails once, succeeds within the configured immediate retry limit, and completes without entering the dead-letter subqueue.
- [x] 5.4 Add a permanently failing handler scenario that exhausts recoverability and verify the dead-letter subqueue contains the expected reason and non-secret diagnostic description.

## 6. Live Outbox Replay Identity Coverage

- [x] 6.1 Add a duplicate-detection-enabled queue scenario that dispatches the same `MiniBusOutboxStoredOperation` more than once through the production transport.
- [x] 6.2 Verify every dispatch attempt uses the stored deterministic outgoing message ID as the broker `MessageId`.
- [x] 6.3 Verify Azure Service Bus duplicate detection recognizes or suppresses the replay within a bounded observation window.

## 7. Documentation And Optional CI

- [x] 7.1 Document the dedicated test namespace recommendation, required send/listen/manage permissions, both environment variables, explicit project/category command, expected Azure cost, and normal-test behavior.
- [x] 7.2 Document bounded timing expectations, configured-failure semantics, secret handling, troubleshooting, and safe cleanup of stale `minibus-live` entities.
- [x] 7.3 Update root and transport documentation so live Azure Service Bus coverage is described as available while automatic production infrastructure provisioning remains deferred.
- [x] 7.4 Add a manually triggered, secret-gated CI workflow that runs only the dedicated live project/category with an overall timeout and without dumping secret-bearing environment values.

## 8. Verification

- [x] 8.1 Run the dedicated integration test project without live configuration and verify helper tests pass while all live tests skip clearly without contacting Azure.
- [x] 8.2 Run the normal repository build and test suite without Azure credentials and verify existing infrastructure-free, emulator, SQL, and Azure Storage behavior is unchanged.
- [x] 8.3 Run the selected live suite against a dedicated Azure Service Bus namespace, verify all scenarios pass, and confirm temporary queues, topics, and subscriptions are removed.
- [x] 8.4 Run strict OpenSpec validation and update the project backlog/status documentation to mark live Azure Service Bus integration coverage complete.
