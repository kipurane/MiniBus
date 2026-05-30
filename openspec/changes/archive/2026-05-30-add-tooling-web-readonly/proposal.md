## Why

MiniBus now has a provider-neutral tooling core, SQL tooling readers, a best-effort SQL timeline, and a CLI, but the local troubleshooting experience is still command-line only. A packaged browser surface would make the existing operational model easier to inspect while preserving the shared core contract between CLI, API, and UI.

## What Changes

- Add `MiniBus.Tooling.Web` as a packaged ASP.NET Core web app for local operational troubleshooting.
- Expose read-only Minimal API endpoints over existing `MiniBus.Tooling.Core` models for inbox, outbox, saga, and message/correlation timeline inspection.
- Serve a React and TypeScript UI from the web app for read-only list, detail, and timeline views.
- Start with SQL-backed tooling through `MiniBus.Tooling.Sql` and make unavailable broker/log/trace sources explicit in timeline responses and UI state.
- Keep the first UI slice read-only; do not add drain, retry, DLQ resubmit, replay, or destructive broker operations.
- Update documentation to replace old deferred Blazor/API wording with the `MiniBus.Tooling.Web` direction.
- Defer Aspire orchestration, Azure Service Bus inspection, structured log/trace/metric querying, authentication/authorization, remote hosted deployment, and mutating operational actions.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `tooling-local-operations`: Adds the packaged read-only web tooling surface, Minimal API boundary, React/TypeScript UI, and read-only safety requirements.

## Impact

- Affected packages: new `src/MiniBus.Tooling.Web` project with ASP.NET Core Minimal API and React/TypeScript client assets.
- Affected existing packages: `MiniBus.Tooling.Core` and `MiniBus.Tooling.Sql` are reused as the API/UI data model and first provider implementation.
- Affected tests: API endpoint tests, read-only safety tests, and UI build or smoke verification for the packaged static assets.
- Affected docs: root README and project context should describe `MiniBus.Tooling.Web`, the read-only first slice, and the future Aspire-local composition path.
- Runtime behavior: normal MiniBus message processing, SQL schema semantics, Service Bus transport behavior, and CLI behavior should not change.
