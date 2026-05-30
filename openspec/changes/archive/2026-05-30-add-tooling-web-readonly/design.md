## Context

The first operational tooling increment established `MiniBus.Tooling.Core`, `MiniBus.Tooling.Sql`, and `MiniBus.Tooling.Cli`. The core package owns provider-neutral records, filters, timeline fragments, reader interfaces, and action contracts. The SQL package maps existing MiniBus SQL inbox, outbox, and saga tables into those models. The CLI proves the read-only inspection flow and JSON/table rendering, but browser-based local troubleshooting remains deferred.

`MiniBus.Tooling.Web` should be the next front door over the same tooling substrate. It needs to be packaged as an ASP.NET Core web app that exposes a Minimal API and serves React/TypeScript assets, while keeping Aspire as a future local orchestration concern rather than a runtime dependency.

```text
                  ┌────────────────────────┐
                  │  MiniBus.Tooling.Core  │
                  │  readers + models      │
                  └───────────┬────────────┘
                              │
          ┌───────────────────┼───────────────────┐
          │                   │                   │
          ▼                   ▼                   ▼
 ┌────────────────┐   ┌────────────────┐   ┌────────────────┐
 │ CLI console    │   │ Tooling Web    │   │ provider       │
 │ scripts/CI/dev │   │ Minimal API    │   │ modules        │
 └────────────────┘   │ + React TS UI  │   └────────────────┘
                      └────────────────┘
```

## Goals / Non-Goals

**Goals:**

- Add `MiniBus.Tooling.Web` as a packaged ASP.NET Core web app.
- Expose read-only Minimal API endpoints for inbox, outbox, saga, and message/correlation timeline inspection.
- Serve a React and TypeScript UI from the web app.
- Keep API responses close to `MiniBus.Tooling.Core` models so CLI, API, and UI describe the same state.
- Compose the first web experience with `MiniBus.Tooling.Sql`.
- Make unavailable sources visible in timeline API responses and UI rather than inferring broker, log, trace, or metrics state.
- Test API behavior, provider composition, and the absence of mutating UI/API operations in the first slice.

**Non-Goals:**

- Aspire orchestration.
- Azure Service Bus inspection or DLQ peek.
- Structured log, trace, metric, Application Insights, or Azure Monitor querying.
- Authentication or authorization.
- Remote deployment templates.
- UI/API actions that mutate state, including outbox drain, retry, DLQ resubmit, replay, or destructive broker operations.
- Runtime message-processing behavior or SQL schema changes.

## Decisions

### 1. Package API and UI Together in `MiniBus.Tooling.Web`

`MiniBus.Tooling.Web` should own both the ASP.NET Core Minimal API host and the built React/TypeScript client assets. The web app becomes the package a developer runs for browser-based MiniBus inspection.

Alternative considered: separate API and UI projects. That makes frontend development familiar, but it creates more packaging and orchestration work before the first UI proves value. A single web app can still use a client subdirectory and a Vite-style development workflow while shipping one MiniBus tooling surface.

### 2. Use Minimal API as the Only Data Boundary for the React UI

The React UI should call local HTTP endpoints rather than reaching directly into .NET services. The API should depend on `MiniBus.Tooling.Core` abstractions and serialize provider-neutral records or thin response envelopes around them.

Alternative considered: render .NET data directly into the page. That would be faster for a narrow UI but would weaken the reusable API boundary needed for future local/remote tooling.

### 3. Start With SQL Provider Composition

The first `MiniBus.Tooling.Web` implementation should configure SQL-backed tooling readers from application configuration, using `MiniBus.Tooling.Sql` as the first provider. Connection strings, schema names, and credentials stay application-owned and must not be printed into UI or logs.

Alternative considered: define a provider plugin system immediately. The project has only one implemented tooling provider today, so a plugin layer would add abstraction before it pays for itself.

### 4. Keep the First Web Surface Read-Only

The first API and UI should expose only list, detail, and timeline inspection. There should be no HTTP endpoints or visible controls for outbox drain, retry, DLQ resubmit, replay, or destructive broker actions.

Alternative considered: expose the existing bounded SQL outbox drain action because the core already models it. The CLI action is useful, but adding browser-triggered mutation would require confirmation UX, authorization guidance, audit expectations, and sharper deployment boundaries. Those should come later.

### 5. Represent Detail Views as Filtered Reads Where Possible

The existing tooling core has list readers and timeline readers, not dedicated detail-reader interfaces. The first API can model detail endpoints by applying the existing message id, correlation id, endpoint, status, and limit filters, returning a single matching record or a not-found response where the provider can answer the filter.

Alternative considered: add new detail interfaces to `MiniBus.Tooling.Core`. Dedicated methods may become useful, but the first API should avoid expanding core contracts unless list-filter semantics prove insufficient.

### 6. Use TypeScript Client Types Generated or Mirrored From API Contracts

The React UI should use explicit TypeScript models for API responses. The first implementation can mirror the C# response shapes manually to keep setup small, with later OpenAPI generation left as an option if the API grows.

Alternative considered: introduce OpenAPI client generation immediately. That can be valuable later, but it adds build complexity before the endpoint set stabilizes.

## Risks / Trade-offs

- [Risk] The web package becomes a second operational model. -> Keep endpoints wired to `MiniBus.Tooling.Core` readers and avoid UI-only interpretations of state.
- [Risk] Browser-based tooling suggests safe remote use before auth exists. -> Document the first slice as local/developer tooling and exclude authentication, authorization, and hosted deployment from this change.
- [Risk] Static asset packaging complicates `dotnet pack`. -> Add explicit build/package verification so the web app serves built assets consistently.
- [Risk] The first UI could overpromise timeline certainty. -> Render source availability and unavailable provider reasons directly in the timeline view.
- [Risk] Node/Vite tooling adds a second toolchain to a .NET repo. -> Keep the client isolated under `MiniBus.Tooling.Web`, use TypeScript for UI correctness, and verify with focused build/smoke tests.
- [Risk] Detail endpoints built from list filters may be awkward. -> Treat this as acceptable for the first slice and revisit dedicated core interfaces only if implementation forces unclear behavior.

## Migration Plan

This change is additive. Existing runtime packages, SQL schema, CLI behavior, samples, and tests continue to work without `MiniBus.Tooling.Web`.

Rollback is removing the new web project, tests, and documentation updates. Because the web surface is read-only and does not change persistence schemas or message processing behavior, rollback should not require data migration.

## Open Questions

- Should the first project expose OpenAPI metadata for the Minimal API, or keep endpoints documented only in README/tests until the API shape settles?
- Should UI static assets be built automatically during `dotnet build`/`dotnet pack`, or should the first implementation require an explicit client build step in verification?
- Should `MiniBus.Tooling.Web` use the same command-line argument style as the CLI for connection strings, or rely only on ASP.NET Core configuration providers?
