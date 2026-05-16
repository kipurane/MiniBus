## Why

MiniBus now has broad component, adapter, SQL, Azure Storage, and sample coverage, but it does not yet have a small high-level confidence layer proving that the advertised setup path composes into a working solution. Before adding observability, MiniBus should first have reference workflows that future tracing and logging can explain.

## What Changes

- Add high-level reference solution acceptance tests that exercise MiniBus through realistic dependency injection composition rather than narrowly constructed internals.
- Add an always-on Tier 1 smoke test that builds a real service provider, resolves `MiniBusProcessor`, processes a realistic billing command message, and verifies the sample-style workflow.
- Verify handler invocation, outgoing command dispatch, event publish, saga timeout scheduling, and successful settlement through recording test doubles.
- Add one SQL-backed Tier 2 acceptance scenario using the existing SQL Server Testcontainers/external-connection-string pattern.
- Verify that SQL-backed processing composes with the same workflow shape and records key persistence effects such as inbox state, outbox capture, and saga state or scheduled outbox work.
- Keep live Azure Service Bus, full Azure Functions host execution, dashboards, manual retry tooling, and observability instrumentation out of scope.

No production API changes are intended unless a small testability adjustment is required to exercise the public setup path.

## Capabilities

### New Capabilities

- `reference-solution-acceptance-tests`: Defines high-level acceptance coverage that proves MiniBus can be assembled through sample-style/public registration and process realistic workflows across core, adapter, transport, saga, and SQL persistence pieces.

### Modified Capabilities

None.

## Impact

- `tests/*` gains a high-level acceptance test layer, either in a new acceptance test project or in an existing test project if that better matches the solution structure.
- The Tier 1 scenario uses fake or recording transport and settlement dependencies so it remains fast and does not require Docker, Azure Service Bus, or a Functions host.
- The Tier 2 scenario reuses the existing SQL Server Testcontainers infrastructure or documented external SQL Server/Azure SQL connection string path.
- `samples/MiniBus.Samples.FunctionApp` may be reused or mirrored by tests so sample-style registration remains covered against composition regressions.
- README or sample test documentation may be updated briefly to explain the new acceptance-test layer.
