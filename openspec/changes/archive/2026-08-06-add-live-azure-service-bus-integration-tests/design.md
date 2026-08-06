## Context

MiniBus currently has fast unit coverage for Azure Service Bus routing, envelope creation, scheduling calls, header mapping, recoverability decisions, and settlement decisions. It also has an emulator-backed Billing-to-Inventory workflow, but the repository has no test layer that exercises the production Azure Service Bus broker. The Azure SDK and service can enforce behaviors that mocks and the emulator do not reproduce exactly, especially entity management, scheduled availability, lock settlement, redelivery, dead-letter metadata, duplicate detection, and system/application property round-tripping.

The live suite must remain optional because it requires a paid external namespace, network access, and credentials with entity-management rights. Normal pull-request validation must remain infrastructure-free. The suite must also be safe for a shared dedicated test namespace: resources need collision-resistant names, bounded lifetimes, and cleanup even when assertions fail.

## Goals / Non-Goals

**Goals:**

- Prove representative MiniBus transport and processing behavior against live Azure Service Bus.
- Make live execution explicit, selectable, bounded, parallel-safe, and diagnosable.
- Reuse production dispatch, serialization, routing, processing, recoverability, and outbox-operation code.
- Isolate every run with temporary broker entities and perform best-effort cleanup.
- Keep credentials and connection details out of test output.
- Provide a documented local path and an optional manual or secret-gated CI path.

**Non-Goals:**

- Replacing unit, emulator, or infrastructure-free acceptance tests.
- Provisioning or deleting Azure Service Bus namespaces.
- Deploying an Azure Functions host.
- Adding runtime APIs or behavior solely for testability.
- Load, throughput, soak, latency benchmarking, or broad Azure fault-injection testing.

## Decisions

### Use a dedicated live integration test project

Create `tests/MiniBus.AzureServiceBus.IntegrationTests` and add it to the solution as a non-packable test project. It will reference the runtime packages needed to assemble the scenarios, including `MiniBus.AzureServiceBus`, `MiniBus.AzureFunctions`, and `MiniBus.Core`; it will not depend on sample applications or create a second reference workflow.

This placement keeps external-resource helpers, administration permissions, longer timeouts, and opt-in conventions out of the fast transport unit project. It is a better fit than `MiniBus.AcceptanceTests` because most assertions target production broker semantics rather than the full sample composition path.

Alternatives considered:

- Add tests to `MiniBus.AzureServiceBus.Tests`. This keeps transport tests together but mixes fast component tests with credentialed broker lifecycle code.
- Add tests to `MiniBus.AcceptanceTests`. This fits cross-package processing scenarios but would blur the existing acceptance tiers and pull transport-specific administration into a sample-oriented project.

### Require explicit opt-in in addition to a connection string

Live facts use a custom attribute or equivalent shared guard with the stable trait `Category=LiveAzureServiceBus`. A test is eligible only when `MINIBUS_RUN_LIVE_AZURE_SERVICEBUS_TESTS=true` and `MINIBUS_AZURE_SERVICEBUS_CONNECTION_STRING` is non-empty. Missing opt-in or configuration produces a clear skip reason without exposing the configured value.

Requiring both values prevents a developer who happens to have a connection string in their shell from incurring cost or mutating a namespace during an ordinary solution test run. Documentation will show an explicit project command and category filter.

Alternative considered: treat the connection string alone as opt-in. This is simpler, but accidental execution is too easy in developer and CI environments.

### Manage temporary entities inside a caller-owned namespace

The suite uses `ServiceBusAdministrationClient` with the supplied connection string to create queues, topics, and subscriptions. The credential therefore requires data-plane send/listen rights and management rights within a dedicated test namespace. The suite does not create or delete namespaces.

A central name factory will:

- use a fixed `minibus-live` prefix;
- include a short run token and scenario token;
- normalize unsupported characters;
- truncate components and append a stable hash where needed;
- apply the most restrictive relevant limit separately for queues, topics, and subscriptions.

Each fixture records only entities it successfully created and deletes them in reverse dependency order during asynchronous disposal. Cleanup is best effort: cleanup failures are attached to diagnostics without replacing the primary test failure. Documentation will include a prefix-based stale-resource cleanup procedure.

Alternative considered: require pre-created shared entities. That supports narrower credentials but creates cross-run interference and makes parallel execution unreliable.

### Centralize bounded broker operations

Shared helpers will wrap receive and state-observation operations in linked cancellation tokens with explicit scenario timeouts. Polling will use short, bounded receive waits and a monotonic deadline; tests will not use fixed multi-second sleeps as their synchronization mechanism. Scheduled-delivery tests may choose a near-future due time, but observation still uses bounded receive polling.

Timeout errors will identify the expected condition, entity alias, elapsed timeout, and last non-sensitive broker state. Helpers will avoid printing the namespace endpoint or connection string.

Alternative considered: use fixed delays followed by a single receive. That is shorter to write but unnecessarily slow when Azure responds quickly and flaky when delivery is slower than expected.

### Treat configured infrastructure failures as failures

Only missing explicit opt-in or missing configuration causes a skip. Once both gates are present, authentication failures, authorization failures, DNS/network failures, entity creation failures, broker timeouts, and assertion mismatches fail the test. This prevents a real regression or broken CI secret from being reported as an innocuous skip.

Exception messages may classify the likely source as configuration, infrastructure, timeout, or assertion, but classification does not convert configured failures into skips.

### Cover representative production broker seams

The suite will use a small number of resource-efficient scenarios:

1. Dispatch a routed command and a routed event through `AzureServiceBusTransportDispatcher`, receive them from a queue and topic subscription, and verify body plus message identity, correlation, causation, message type, and custom application properties.
2. Schedule a routed message, verify it is not delivered before the allowed timing tolerance, then receive it within a bounded deadline with metadata preserved.
3. Use a test-only `IMiniBusMessageActions` implementation backed by `ServiceBusReceiver` to run `MiniBusProcessor` without an Azure Functions host. Verify successful completion removes the locked message and exhausted recoverability dead-letters it with the expected reason and description.
4. Abandon a locked message through the test receiver to induce broker redelivery, then verify redelivery before processing it through the MiniBus retry/completion path. Abandon remains test orchestration, not a new MiniBus settlement API.
5. Dispatch the same `MiniBusOutboxStoredOperation` more than once to a duplicate-detection-enabled queue and verify each attempt uses the same outgoing `MessageId`, allowing the broker to suppress the duplicate.

These scenarios reuse runtime code and add only test fixtures, test handlers, and receiver-backed settlement plumbing. Lower-level permutations remain in unit tests.

### Keep parallelism through isolation, with targeted serialization only where required

Entity names are unique per scenario so independent test classes can run in parallel. Tests that share an entity or intentionally inspect duplicate detection will serialize only within their fixture or xUnit collection. The suite will not disable parallel execution globally unless live evidence shows an Azure quota or SDK constraint that cannot be isolated.

### Make CI opt-in and non-blocking for ordinary pull requests

Add an optional workflow design, preferably a manually triggered GitHub Actions workflow with an environment-protected secret. A scheduled run may be added if repository policy supports it. The ordinary pull-request workflow will not require Azure credentials and will not set the live opt-in variable.

The live workflow will run only the dedicated project with the live category filter, enforce an overall timeout, and avoid verbose environment dumps.

## Risks / Trade-offs

- [Risk] Azure availability or transient network failures can make live tests noisy. → Mitigation: use bounded SDK-aware operations, actionable failure classification, a small scenario set, and optional retry only for idempotent setup/observation operations—not for product assertions.
- [Risk] Failed or cancelled runs can leave billable entities behind. → Mitigation: use a recognizable prefix, fixture-owned resource registries, reverse-order best-effort cleanup, and documented stale-resource cleanup.
- [Risk] Parallel runs can collide or exceed namespace quotas. → Mitigation: use collision-resistant names, minimize entities, serialize only shared-resource scenarios, and document use of a dedicated namespace.
- [Risk] Timing assertions for scheduled messages can be flaky because clocks and service delivery are not exact. → Mitigation: assert against a tolerance window and bounded eventual delivery rather than exact timestamps.
- [Risk] A management connection string grants broad rights. → Mitigation: require a dedicated test namespace, protect secrets in CI, never log connection values, and document the minimum practical rights.
- [Trade-off] A dedicated project adds solution and package-reference maintenance. → It provides a much clearer execution and security boundary than mixing live tests into fast suites.
- [Trade-off] Duplicate-detection validation adds broker wait time to establish that a second message is absent. → Keep the absence window bounded and focused on one representative replay scenario.

## Migration Plan

1. Add the dedicated non-packable integration test project and shared live-test guard/fixtures.
2. Add entity lifecycle, naming, bounded polling, and secret-safe diagnostic helpers.
3. Add transport, scheduling, settlement/recoverability, and outbox replay scenarios.
4. Add the project to the solution while keeping live execution disabled by default.
5. Update root and transport testing documentation.
6. Add the optional manual or secret-gated CI workflow after local execution is verified.

Rollback consists of removing the dedicated project, workflow, and documentation. No runtime migration or persisted data migration is required.

## Open Questions

None required before implementation. A repository administrator may choose whether the optional CI workflow is manual-only or also scheduled; this does not change the suite contract.
