## ADDED Requirements

### Requirement: Packaged Tooling Web Surface
MiniBus SHALL provide `MiniBus.Tooling.Web` as a packaged ASP.NET Core web app that combines a Minimal API with served React and TypeScript UI assets for local operational troubleshooting.

#### Scenario: Tooling web app is available as a package
- **WHEN** a developer adds or runs the `MiniBus.Tooling.Web` tooling surface
- **THEN** the web app exposes the read-only tooling API and serves the browser UI from the same application package

#### Scenario: Aspire remains an orchestration concern
- **WHEN** local samples later use Aspire to run SQL Server, Service Bus emulator, Function Apps, dispatcher hosts, and tooling together
- **THEN** `MiniBus.Tooling.Web` does not require Aspire as a runtime dependency

### Requirement: Read-Only Tooling Minimal API
MiniBus SHALL expose read-only Minimal API endpoints over `MiniBus.Tooling.Core` contracts for inbox, outbox, saga, and message/correlation timeline inspection.

#### Scenario: List operational records through the API
- **WHEN** a caller requests inbox, outbox, or saga records through the tooling API with supported filters
- **THEN** MiniBus returns records represented by the shared tooling read models or thin API envelopes around those models

#### Scenario: Show operational record details through the API
- **WHEN** a caller requests a specific inbox, outbox, or saga detail view through the tooling API
- **THEN** MiniBus returns the matching tooling record when the configured provider can answer the query or a not-found response when no matching record exists

#### Scenario: Show message timeline through the API
- **WHEN** a caller requests a timeline for a message id
- **THEN** MiniBus returns the best-effort timeline assembled from configured tooling providers without requiring a live Azure Service Bus namespace

#### Scenario: Show correlation timeline through the API
- **WHEN** a caller requests a timeline for a correlation id
- **THEN** MiniBus returns timeline fragments ordered by available timestamps and includes source availability metadata

#### Scenario: Configure SQL as the first provider
- **WHEN** `MiniBus.Tooling.Web` is configured with SQL tooling settings
- **THEN** the API uses `MiniBus.Tooling.Sql` readers to answer inbox, outbox, saga, and timeline requests

### Requirement: Tooling Web UI
MiniBus SHALL serve a React and TypeScript UI that uses the read-only Minimal API for browser-based local troubleshooting.

#### Scenario: Browse operational lists
- **WHEN** a developer opens the tooling UI
- **THEN** the UI provides read-only list views for inbox, outbox, and saga records using the tooling API

#### Scenario: Inspect operational details
- **WHEN** a developer selects an inbox, outbox, or saga record in the tooling UI
- **THEN** the UI shows a read-only detail view using data returned by the tooling API

#### Scenario: Inspect correlated timeline
- **WHEN** a developer searches by message id or correlation id in the tooling UI
- **THEN** the UI shows the corresponding timeline fragments and available source metadata from the tooling API

#### Scenario: Display unavailable sources
- **WHEN** broker, log, trace, audit, metrics, or other tooling sources are not configured
- **THEN** the UI displays those sources as unavailable instead of inferring their state from SQL data

### Requirement: Web Tooling Read-Only Safety
MiniBus tooling SHALL keep the first web API and UI slice read-only and exclude mutating operational actions.

#### Scenario: Web API excludes mutating actions
- **WHEN** a caller interacts with the first `MiniBus.Tooling.Web` API surface
- **THEN** the API does not expose endpoints for outbox drain, retry, DLQ resubmit, message replay, destructive broker operations, or other state-changing actions

#### Scenario: Web UI excludes mutating controls
- **WHEN** a developer uses the first tooling UI
- **THEN** the UI does not show controls for outbox drain, retry, DLQ resubmit, message replay, destructive broker operations, or other state-changing actions

#### Scenario: Read-only web requests do not mutate state
- **WHEN** a developer uses the tooling API or UI to list records, inspect details, or view timelines
- **THEN** MiniBus does not modify inbox, outbox, saga, broker, or runtime state

#### Scenario: Sensitive values remain protected by default
- **WHEN** a developer uses the tooling API or UI
- **THEN** MiniBus does not print connection strings, credentials, full message bodies, or full saga data by default

### Requirement: Web Tooling Documentation And Verification
MiniBus SHALL document and verify the packaged read-only tooling web surface.

#### Scenario: Documentation describes Tooling Web
- **WHEN** a developer reads MiniBus tooling documentation
- **THEN** it describes `MiniBus.Tooling.Web`, its read-only first slice, SQL-backed configuration, React and TypeScript UI, and deferred Aspire-local composition path

#### Scenario: API behavior is verified
- **WHEN** the tooling web tests run
- **THEN** they verify read-only API behavior for list, detail, and timeline requests over configured tooling readers

#### Scenario: Web package behavior is verified
- **WHEN** the tooling web verification runs
- **THEN** it verifies that the web app can serve the built UI assets and does not expose mutating operational endpoints in the first slice
