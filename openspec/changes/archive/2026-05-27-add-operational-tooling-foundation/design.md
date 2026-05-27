## Context

MiniBus has established the main runtime baseline: message processing, Azure Service Bus transport, Azure Functions adapters, SQL inbox/outbox/saga persistence, outbox dispatch, observability, samples, and acceptance coverage. The next gap is local operational understanding. Developers can inspect SQL manually or read logs, but there is no MiniBus-owned model for answering common questions such as "was this message processed?", "what outbox operations are pending or failed?", or "what saga state exists for this correlation?".

The project context already points toward a shared tooling substrate with CLI and future UI/API front doors over the same core. This change starts that path with the smallest useful slice: provider-neutral contracts, SQL-backed readers, and a CLI suitable for local troubleshooting and scripted diagnostics.

## Goals / Non-Goals

**Goals:**

- Add `MiniBus.Tooling.Core` for provider-neutral read models, filters, timeline fragments, and explicit action contracts.
- Add `MiniBus.Tooling.Sql` for inbox, outbox, and saga readers over the existing SQL schema.
- Add `MiniBus.Tooling.Cli` as the first user-facing tool over the shared core and SQL provider.
- Support filtering by endpoint, message id, correlation id, status, and time window where the underlying data can answer it.
- Support an explicit bounded SQL outbox drain action that reuses `SqlMiniBusOutboxDispatcher`.
- Keep the design compatible with later Minimal API, Blazor UI, broker providers, and observability providers.
- Document safety boundaries, redaction expectations, and credential handling for local tooling.

**Non-Goals:**

- Build a Blazor UI, Minimal API, or Aspire orchestration in this change.
- Inspect Azure Service Bus entities, peek DLQs, resubmit messages, or replay messages.
- Query Application Insights, Azure Monitor, arbitrary console output, or custom application logs.
- Add a durable MiniBus audit/event store.
- Change runtime message-processing behavior, SQL schema semantics, or Service Bus transport behavior.
- Add background or daemon-style tooling behavior.

## Decisions

### 1. Split Tooling Into Core, SQL, And CLI Projects

`MiniBus.Tooling.Core` owns the reusable contracts:

- `MiniBusInboxRecord`
- `MiniBusOutboxRecord`
- `MiniBusSagaRecord`
- `MiniBusMessageTimeline`
- query filter objects
- reader/action interfaces
- action result models

`MiniBus.Tooling.Sql` implements those interfaces over the existing SQL tables and composes with `MiniBus.Persistence.Sql` for bounded outbox drain actions.

`MiniBus.Tooling.Cli` is a thin executable front door that parses arguments, configures providers, invokes core services, and renders stable human-readable output.

Alternative considered: put the first CLI directly in `MiniBus.Persistence.Sql`. That would be faster, but it would make CLI concepts SQL-specific and force future UI/API work either to duplicate models or depend on a persistence package as an accidental tooling core.

### 2. Treat SQL As The First Provider, Not The Tooling Model

The SQL provider should map existing SQL rows into provider-neutral records. The core model can include SQL-informed fields such as processed timestamps, operation kind, attempt count, claim state, dispatch state, saga data type, completion state, and row version metadata, but SQL table names and query construction remain provider-specific.

Alternative considered: expose raw SQL rows through the CLI. That helps debugging one database today but creates a poor contract for future UI/API work and for non-SQL providers.

### 3. Start With Read-First Tooling And One Explicit Action

Most commands should be read-only:

- list inbox records
- list outbox records
- list saga records
- show message or correlation details from available SQL state

The only first action should be bounded SQL outbox drain. It must require explicit command invocation and explicit bounds such as maximum batches or batch size. It must reuse `SqlMiniBusOutboxDispatcher` so claim leases, retry metadata, deterministic outgoing ids, and at-least-once semantics remain centralized.

Alternative considered: add failed-message retry, DLQ resubmit, or replay commands immediately. Those are operationally useful but need stronger safety, authorization, audit, and message mutation requirements than this first local tooling slice should carry.

### 4. Timeline Is Best-Effort From Available State

The first timeline should be a simple aggregation over available SQL state, not a full event history. For a message id or correlation id, tooling can show inbox records, related outbox operations, and saga records that share known identifiers. It should clearly represent missing provider data as unavailable rather than inferring broker or log outcomes.

Alternative considered: require logs/traces/audit data before adding timelines. That would make the first tooling increment too dependent on unresolved observability-source decisions. A best-effort SQL timeline is still useful and sets the shape for adding providers later.

### 5. CLI Output Should Be Stable Enough For Humans And Scripts

The CLI should default to compact table-style output for local use and include a machine-readable output option such as JSON. Column names and JSON property names should come from tooling core models where practical.

Alternative considered: human output only. That keeps the CLI small, but it misses the project goal of repeatable troubleshooting, scripts, and CI diagnostics.

### 6. Redaction Is A Boundary, Not An Afterthought

The first SQL reader should avoid dumping full message bodies by default. Headers, saga data, and error details can include sensitive values, so the core should expose enough metadata for safe summaries and leave full serialized inspection behind explicit flags or future redaction policy support. Documentation should state that credentials come from normal application-owned configuration and should not be printed.

Alternative considered: show everything stored in SQL because the user already has database access. That makes local debugging convenient but creates bad defaults for screenshots, CI logs, and future UI/API surfaces.

## Risks / Trade-offs

- [Risk] Tooling core becomes too abstract before there is more than one provider. -> Keep interfaces small and driven by SQL-backed use cases that are already known, while avoiding SQL names in core contracts.
- [Risk] Timeline output implies more certainty than SQL state can provide. -> Label source and status clearly, and avoid claiming broker/log state until provider modules exist.
- [Risk] CLI grows into a second runtime surface. -> Restrict first actions to explicit, bounded commands and route dispatch through existing runtime services.
- [Risk] Query filters differ across data types. -> Use shared filter concepts where possible, but allow provider/read-model-specific unsupported filters to fail clearly.
- [Risk] Saga data and headers leak sensitive values. -> Default to summaries, document redaction expectations, and make full detail opt-in or deferred.
- [Risk] Connection-string handling becomes inconsistent with runtime packages. -> Reuse existing SQL registration patterns where possible and keep credentials application-owned.

## Migration Plan

This is additive. Existing runtime packages, samples, and applications continue to work without tooling. The first implementation should add projects to the solution, add tests, document CLI usage, and update project status/backlog after the change is complete.

Rollback is removing the new tooling packages/projects and documentation. No runtime schema migration or application behavior change is expected.

## Open Questions

- Should the CLI be packaged as a local .NET tool, a normal console application, or both?
- What machine-readable output modes should be supported in the first slice: JSON only, or JSON plus NDJSON for streaming use?
- How much saga data inspection should the first implementation expose before a deeper redaction model exists?
