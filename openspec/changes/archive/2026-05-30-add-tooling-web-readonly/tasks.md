## 1. Project Structure

- [x] 1.1 Add `src/MiniBus.Tooling.Web` as an ASP.NET Core web project with package metadata consistent with the existing MiniBus projects.
- [x] 1.2 Add `MiniBus.Tooling.Web` to `MiniBus.sln` and configure project references to `MiniBus.Tooling.Core` and `MiniBus.Tooling.Sql`.
- [x] 1.3 Add `tests/MiniBus.Tooling.Web.Tests` with solution entry and references needed for API and packaging-focused tests.
- [x] 1.4 Add a React and TypeScript client directory under `MiniBus.Tooling.Web` with package scripts for build and local development.

## 2. Web Host And Provider Composition

- [x] 2.1 Implement ASP.NET Core startup composition for `MiniBus.Tooling.Web` without changing existing runtime package behavior.
- [x] 2.2 Bind SQL tooling configuration from ASP.NET Core configuration, including connection string and schema name.
- [x] 2.3 Register SQL-backed inbox, outbox, saga, and timeline readers through `MiniBus.Tooling.Core` interfaces.
- [x] 2.4 Ensure connection strings, credentials, full message bodies, and full saga data are not printed by default.

## 3. Minimal API

- [x] 3.1 Add read-only API endpoints for inbox, outbox, and saga list queries using supported tooling filters.
- [x] 3.2 Add read-only API endpoints for inbox, outbox, and saga detail queries using existing tooling reader/filter semantics.
- [x] 3.3 Add read-only API endpoints for message-id and correlation-id timeline queries.
- [x] 3.4 Return source availability metadata in timeline responses so unavailable broker, log, trace, audit, and metrics sources are explicit.
- [x] 3.5 Ensure the first API surface exposes no routes for outbox drain, retry, DLQ resubmit, message replay, destructive broker operations, or other state-changing actions.

## 4. React And TypeScript UI

- [x] 4.1 Implement typed API client models for the read-only tooling API responses.
- [x] 4.2 Build inbox, outbox, and saga list views with filter controls backed by the Minimal API.
- [x] 4.3 Build read-only detail views for selected inbox, outbox, and saga records.
- [x] 4.4 Build message/correlation timeline search and timeline display using API timeline responses.
- [x] 4.5 Display unavailable source state in the timeline UI without inferring missing broker, log, trace, audit, or metrics data.
- [x] 4.6 Verify the UI exposes no controls for mutating operational actions in the first slice.

## 5. Packaging And Static Assets

- [x] 5.1 Configure the web app to serve built React/TypeScript assets from the ASP.NET Core host.
- [x] 5.2 Include the built UI assets in the packaged `MiniBus.Tooling.Web` output.
- [x] 5.3 Document or automate the required client build step for local verification and packaging.

## 6. Tests

- [x] 6.1 Add API tests for inbox, outbox, saga, and timeline read-only endpoint behavior using test doubles for tooling readers.
- [x] 6.2 Add tests that detail endpoints return matching records or not-found responses without mutating tooling state.
- [x] 6.3 Add tests that the first API surface does not expose mutating operational routes.
- [x] 6.4 Add verification for static asset serving or packaged asset presence.
- [x] 6.5 Add TypeScript/client build verification for the React UI.

## 7. Documentation

- [x] 7.1 Update `README.md` operational tooling documentation to describe `MiniBus.Tooling.Web` and remove old deferred Blazor/API wording.
- [x] 7.2 Add `MiniBus.Tooling.Web` package README with setup, SQL configuration, local-only safety notes, and first-slice limitations.
- [x] 7.3 Update `openspec/project.md` backlog/status to reflect the implemented web tooling slice once complete.
- [x] 7.4 Document that Aspire local orchestration remains future work and is not a runtime dependency of `MiniBus.Tooling.Web`.

## 8. Verification

- [x] 8.1 Run focused `MiniBus.Tooling.Web` and tooling test suites.
- [x] 8.2 Run the React/TypeScript client build.
- [x] 8.3 Run repository build or package verification needed to prove `MiniBus.Tooling.Web` is packable with its static assets.
