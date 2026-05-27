## Why

MiniBus now has enough runtime reliability features that the next production-readiness gap is operability: developers can process, persist, retry, and dispatch messages, but they do not yet have a first-class way to inspect MiniBus state during local troubleshooting. A small tooling foundation gives the project a shared model for future CLI, API, and UI work without jumping straight to a dashboard.

## What Changes

- Add a provider-neutral tooling core that defines read models, query filters, and explicit action contracts for MiniBus operational state.
- Add SQL-backed tooling readers for inbox, outbox, and saga state using the existing SQL schema as the first provider implementation.
- Add simple correlated message timeline fragments from available SQL state so developers can inspect what MiniBus knows about a message or correlation.
- Add a CLI-first troubleshooting surface for listing inbox, outbox, and saga records; showing message/correlation details; and running an explicit bounded SQL outbox drain.
- Reuse the existing `SqlMiniBusOutboxDispatcher` for outbox drain actions instead of adding another dispatch implementation.
- Document the safety boundary between read-only inspection and explicit actions, including redaction and credential-handling expectations.
- Defer UI, HTTP API, Aspire orchestration, broker inspection, destructive operations, log querying, and new runtime processing behavior.

## Capabilities

### New Capabilities

- `tooling-local-operations`: Operational tooling contracts and the first SQL/CLI implementation for local MiniBus troubleshooting.

### Modified Capabilities

None.

## Impact

- Affected packages: new `MiniBus.Tooling.Core`, `MiniBus.Tooling.Sql`, and `MiniBus.Tooling.Cli` projects.
- Affected runtime packages: `MiniBus.Persistence.Sql` may be referenced by tooling for dispatcher reuse, but normal message processing behavior should not change.
- Affected samples/docs: local troubleshooting documentation should explain the CLI, SQL connection configuration, safe bounded actions, and what remains deferred.
- Affected tests: unit tests for provider-neutral filters/read models, SQL reader tests over the existing schema, CLI command parsing/output behavior, and bounded outbox drain behavior using the existing dispatcher.
