## 1. Project Structure

- [x] 1.1 Add `MiniBus.Tooling.Core`, `MiniBus.Tooling.Sql`, and `MiniBus.Tooling.Cli` projects with solution entries and package metadata consistent with existing MiniBus projects.
- [x] 1.2 Add matching test projects for tooling core, SQL tooling, and CLI behavior.
- [x] 1.3 Wire project references so the CLI depends on tooling core and SQL tooling, SQL tooling depends on tooling core and the existing SQL persistence package where dispatcher reuse is needed, and tooling core has no provider-specific dependencies.

## 2. Tooling Core Contracts

- [x] 2.1 Define provider-neutral read models for inbox records, outbox records, saga records, timeline fragments, and timeline source availability.
- [x] 2.2 Define query filter objects for endpoint, message id, correlation id, status, and time-window filtering.
- [x] 2.3 Define reader interfaces for inbox, outbox, saga, and timeline queries.
- [x] 2.4 Define action contracts and result models for explicit tooling actions, including bounded outbox drain.
- [x] 2.5 Add unit tests for filter validation, unsupported-filter results, status modeling, and timeline fragment ordering.

## 3. SQL Tooling Provider

- [x] 3.1 Implement SQL inbox reader mapping existing `MiniBus.Inbox` rows to tooling core inbox records.
- [x] 3.2 Implement SQL outbox reader mapping existing `MiniBus.Outbox` rows to tooling core outbox records with derived pending, claimed, dispatched, and failed status.
- [x] 3.3 Implement SQL saga reader mapping existing `MiniBus.Sagas` rows to tooling core saga records without dumping full saga data by default.
- [x] 3.4 Implement SQL-backed filter handling for endpoint, message id, correlation id, status, and time windows where supported.
- [x] 3.5 Implement best-effort SQL timeline assembly for message id and correlation id using available inbox, outbox, and saga state.
- [x] 3.6 Add SQL integration tests over the existing schema for readers, filters, unsupported filters, and timeline assembly.

## 4. Bounded Outbox Drain Action

- [x] 4.1 Implement a tooling action that invokes the existing `SqlMiniBusOutboxDispatcher` with explicit bounds.
- [x] 4.2 Ensure the action reports dispatched counts and failure information without changing SQL outbox dispatch semantics.
- [x] 4.3 Add tests proving the tooling action reuses existing dispatcher behavior and does not start background dispatch.

## 5. CLI Surface

- [x] 5.1 Add CLI commands to list inbox, outbox, and saga records with common filters.
- [x] 5.2 Add CLI commands to show message-id and correlation-id details from the best-effort SQL timeline.
- [x] 5.3 Add a CLI command for bounded SQL outbox drain with explicit bounds.
- [x] 5.4 Add compact human-readable output for list, show, and action commands.
- [x] 5.5 Add machine-readable JSON output for list, show, and action commands.
- [x] 5.6 Add CLI tests for command parsing, output shape, filter forwarding, action bounds, and read-only command behavior.

## 6. Safety And Documentation

- [x] 6.1 Document local SQL connection configuration and credential-handling expectations for tooling.
- [x] 6.2 Document read-only commands versus explicit actions, including bounded outbox drain behavior.
- [x] 6.3 Document default redaction behavior and the limits of first-slice saga/header/error inspection.
- [x] 6.4 Document deferred surfaces: Blazor UI, Minimal API, Aspire orchestration, Azure Service Bus inspection, DLQ resubmission, message replay, arbitrary log scraping, and Azure Monitor querying.
- [x] 6.5 Update `openspec/project.md` backlog/status after implementation is complete.

## 7. Verification

- [x] 7.1 Run focused tooling unit tests.
- [x] 7.2 Run SQL tooling integration tests with the existing SQL Server/Testcontainers or external connection-string path.
- [x] 7.3 Run CLI tests and a build of the full solution.
- [x] 7.4 Run `openspec validate --all`.
