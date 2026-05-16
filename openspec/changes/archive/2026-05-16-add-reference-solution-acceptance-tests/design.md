## Context

MiniBus currently has strong narrow coverage: core message handling, Azure Functions processor behavior, Azure Service Bus transport mapping, SQL persistence, Azure Storage persistence, and a buildable Function App sample. The remaining gap is confidence that the advertised setup path composes into a working solution when the pieces are wired together in a realistic service provider.

The sample registration path under `samples/MiniBus.Samples.FunctionApp` demonstrates the intended developer experience, but tests do not yet prove that this setup can process a representative message end to end. Existing processor tests build service providers and verify many behaviors, but they are still processor-focused and hand-assemble test-specific handlers, routes, serializers, and transport doubles.

This change adds an acceptance-test layer that acts as a canary for the framework's reference solution path. It should remain small, fast where possible, and useful as a future baseline for observability acceptance tests.

## Goals / Non-Goals

**Goals:**

- Add an always-on Tier 1 composition smoke test that proves a sample-style MiniBus solution can be assembled and can process a realistic billing command.
- Exercise the public or sample-style registration path rather than recreating every dependency inline in a processor unit test.
- Verify the expected cross-package workflow: message deserialization, handler invocation, outgoing command send, event publish, saga timeout schedule, and message settlement.
- Add one SQL-backed Tier 2 acceptance scenario using the existing SQL Server Testcontainers or external connection string strategy.
- Verify SQL persistence effects at the reference workflow level, including inbox recording, outbox capture, and saga persistence or scheduled outbox operation.
- Keep the scenarios small enough that failure points are easy to understand and future observability work can attach traces/logs to the same workflows.

**Non-Goals:**

- Running a real Azure Functions host.
- Connecting to live Azure Service Bus.
- Testing every processing branch, retry branch, claim-check branch, or audit branch.
- Adding observability instrumentation.
- Creating dashboard/manual retry tooling.
- Changing production behavior unless a small testability gap blocks the reference workflow.

## Decisions

### Add a distinct acceptance-test layer

Create high-level acceptance coverage either in a new test project, such as `tests/MiniBus.AcceptanceTests`, or in the most suitable existing test project if that better fits the solution. Prefer a new project if it avoids overloading `MiniBus.AzureFunctions.Tests` with cross-package reference-solution scenarios.

Alternative considered: keep adding cases to `MiniBusProcessorTests`. That would be faster initially, but it blurs processor behavior tests with solution-level composition tests and keeps the missing confidence layer hidden.

### Base Tier 1 on sample-style registration

The Tier 1 test should reuse or closely mirror `MiniBus.Samples.FunctionApp` registration so it catches sample drift, missing registrations, wrong service lifetimes, route gaps, and serializer/handler/saga setup mistakes. If the current sample uses a throwing placeholder sender, the test can override only the transport sender and settlement actions with recording test doubles while preserving the rest of the sample-style configuration.

Alternative considered: manually register a separate test-only billing solution. That would prove MiniBus can be wired, but it would not protect the advertised sample path as well.

### Keep Tier 1 always-on and infrastructure-free

Tier 1 should use `ServiceBusModelFactory` to create received messages, a recording `IAzureServiceBusSender`, and recording `IMiniBusMessageActions`. It should not require Docker, live Azure resources, or a Functions host. This keeps it suitable for the normal test loop.

Alternative considered: use real Service Bus locally or run the Functions host. That would test more hosting infrastructure, but it would make the canary slow and environment-dependent.

### Add exactly one SQL-backed Tier 2 workflow first

Tier 2 should reuse the existing SQL Server fixture pattern from `MiniBus.Persistence.Sql.Tests`: external connection string when configured, otherwise Testcontainers when Docker is available, otherwise skip with a clear reason. The scenario should process the same or equivalent billing message with SQL persistence enabled and assert durable effects after processing.

The first SQL acceptance scenario should avoid becoming a broad SQL retest. The SQL persistence suite already verifies schema details, duplicate detection, outbox behavior, cleanup, and concurrency. This acceptance test should verify composition at the workflow boundary.

Alternative considered: add many SQL acceptance scenarios for retries, duplicates, cleanup, and failures. That would overlap heavily with existing SQL integration coverage and make the high-level suite expensive too soon.

### Use acceptance tests as future observability anchors

The tests should be written with clear workflow names and assertions so a later observability change can add expectations around logs, activities, metrics, or diagnostic metadata for the same paths. This change should not introduce instrumentation yet.

Alternative considered: combine observability and acceptance tests in one change. That would make it harder to tell whether failures come from workflow composition or telemetry behavior.

## Risks / Trade-offs

- [Risk] Reusing the sample directly may require overriding sample placeholder services. -> Mitigation: keep overrides narrow and document them in test helper names.
- [Risk] A new acceptance test project adds references across several packages and the sample project. -> Mitigation: keep it test-only and avoid production dependency changes.
- [Risk] SQL-backed acceptance tests can be slower or skipped on machines without Docker. -> Mitigation: keep only one Tier 2 scenario and reuse the existing clear skip/external connection string strategy.
- [Risk] The acceptance tests could duplicate existing unit/integration assertions. -> Mitigation: assert only cross-package workflow outcomes, not low-level persistence or transport internals already covered elsewhere.
- [Risk] A sample registration change could break tests even when production packages still work. -> Mitigation: that is intentional when the sample is the advertised setup path; the failure should prompt updating either the sample or the acceptance test expectation.

## Migration Plan

1. Add the acceptance test project or chosen test location to the solution.
2. Add Tier 1 infrastructure-free workflow coverage using sample-style registration and recording test doubles.
3. Add one SQL-backed Tier 2 workflow using existing SQL Server integration infrastructure patterns.
4. Add brief documentation or README notes if useful so contributors understand the purpose and environment expectations.
5. Run the normal test suite and the SQL-backed acceptance scenario where Docker or an external SQL Server/Azure SQL connection is available.

Rollback is low risk: remove the acceptance test project or added test files and any test-only documentation. No production API or data migration is intended.

## Open Questions

- Should the tests reference the sample project directly, or should they mirror the sample setup inside test fixtures to avoid a sample-to-test project dependency?
- Should the SQL-backed acceptance scenario use SQL saga persistence in the same run, or focus first on inbox and outbox capture with saga timeout schedule as the durable scheduled operation?
- Should the Tier 2 test live beside the existing SQL integration tests to reuse internal fixture helpers, or should those helpers be extracted into a shared test utility?
