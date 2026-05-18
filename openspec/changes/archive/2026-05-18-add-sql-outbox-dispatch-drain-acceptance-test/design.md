## Context

MiniBus already has a Tier 1 infrastructure-free reference acceptance test and a Tier 2 SQL-backed reference acceptance test. The SQL-backed test processes the sample billing workflow with SQL persistence enabled and verifies durable inbox, outbox, and saga rows, but it does not run the SQL outbox dispatcher afterward.

Lower-level SQL persistence and outbox dispatcher tests already cover claim/mark/failure mechanics. This change should add one higher-level composition proof: reference workflow processing captures outgoing work into SQL, and the configured dispatcher can later drain that work through the transport abstraction without involving live Azure Service Bus.

## Goals / Non-Goals

**Goals:**

- Prove the SQL-backed reference workflow can be drained end to end through `SqlMiniBusOutboxDispatcher.DispatchPendingAsync`.
- Verify the recording transport sees the expected billing receipt command, domain event, and saga timeout schedule after the drain.
- Verify dispatched SQL outbox rows are no longer pending/reclaimable.
- Reuse existing SQL acceptance infrastructure and environment gating.

**Non-Goals:**

- Add live Azure Service Bus integration tests.
- Add new runtime APIs, dispatcher abstractions, SQL schema, or production behavior.
- Re-test every low-level SQL outbox state transition already covered by SQL persistence tests.
- Expand the reference billing sample into a larger runnable application.

## Decisions

### Extend the existing Tier 2 SQL acceptance fixture

The new scenario should live with `SqlBackedReferenceSolutionAcceptanceTests` and reuse the existing SQL Server fixture, schema application helper, sample-style service registration, and `RecordingServiceBusSender`.

Alternative considered: create a separate acceptance test project or fixture just for outbox draining. That would add structure without adding confidence; the current fixture already models the exact workflow and environment behavior this test needs.

### Drain through the real SQL dispatcher and transport dispatcher

The test should resolve `SqlMiniBusOutboxDispatcher` from the service provider and let it call the configured `IMiniBusOutboxDispatcher`, which in turn uses the Azure Service Bus transport dispatcher against the recording sender. This preserves the same composition path an application would use while avoiding live Azure resources.

Alternative considered: query outbox rows and call lower-level dispatch APIs directly. That would duplicate existing unit/integration coverage and miss the service-provider composition proof.

### Assert high-level dispatch and pending-state outcomes

Assertions should focus on the acceptance-level contract: expected outgoing operations reach the recording sender/scheduler, dispatch count matches the captured operations, and SQL no longer exposes those rows as pending work. The test may use small fixture helpers for dispatched/pending counts, but should avoid asserting internal retry timestamps or every column.

Alternative considered: assert the full SQL row shape after dispatch. That belongs in SQL persistence tests, not reference acceptance coverage.

## Risks / Trade-offs

- [Risk] The test could become too coupled to SQL outbox internals. -> Mitigation: assert only dispatched/pending state needed to prove drain completion, plus transport-visible outcomes.
- [Risk] The acceptance suite gains another SQL-backed path that can be slower or infrastructure-gated. -> Mitigation: reuse the existing `SqlServerFact` behavior and fixture; keep the test focused.
- [Risk] Recording transport assertions may duplicate Tier 1 expectations. -> Mitigation: in this scenario, the important distinction is that transport receives messages only after dispatcher drain, not during processing.
