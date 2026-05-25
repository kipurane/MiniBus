## Why

MiniBus now has two SQL outbox drain paths: applications can drain manually through `SqlMiniBusOutboxDispatcher`, or opt into an in-process hosted service through `AddMiniBusSqlHostedOutboxDispatch(...)`. The hosted service is useful for generic worker-style deployments, but Azure Functions applications have a clearer native scheduling primitive: a timer-triggered Function that runs a bounded outbox drain.

The current Billing sample demonstrates SQL-backed outbox capture and manual CLI draining. The next useful backlog slice is a Functions-native dispatcher reference path that keeps outbox dispatch application-owned while avoiding reliance on always-running background hosted services inside the Functions worker process.

## What Changes

- Add a timer-triggered SQL outbox dispatcher reference path for Azure Functions isolated worker applications.
- Prefer a separate dispatcher Function App in the reference solution so the operational boundary between message processing and outbox draining is obvious.
- Document that colocating the timer trigger in the existing processing Function App remains acceptable for small deployments when the application intentionally wants one host boundary.
- Reuse the existing `SqlMiniBusOutboxDispatcher` and transport registrations instead of adding a second SQL outbox dispatch implementation.
- Keep hosted-service dispatch and manual dispatch as supported alternatives.
- Verify the timer-triggered drain path without requiring live Azure Service Bus or Azure Functions infrastructure in the normal test suite.

## Capabilities

### New Capabilities
None.

### Modified Capabilities
- `buildable-functionapp-sample`: add a timer-triggered SQL outbox dispatcher reference path and document colocated versus separate dispatcher host tradeoffs.
- `sql-inbox-outbox`: document timer-triggered outbox dispatch as a supported application-owned scheduling model alongside manual and hosted-service dispatch.

## Impact

- Affected packages: likely sample projects and documentation first; runtime package changes are only expected if implementation uncovers a small reusable helper worth exposing.
- Affected samples: Billing SQL-backed reference workflow and possibly a new sibling dispatcher Function App sample.
- Affected tests: sample build/registration coverage and acceptance-style coverage for bounded timer drain behavior using existing dispatcher abstractions.
- Operational impact: gives Azure Functions users a clearer first production shape for SQL outbox dispatch while preserving at-least-once dispatch and SQL claim-lease recovery semantics.
