## Purpose

Define the first local operational tooling surface for MiniBus, including provider-neutral tooling contracts, SQL-backed inspection capabilities, a CLI entry point, and clear safety boundaries for read-only inspection versus explicit actions.

## Requirements

### Requirement: Provider-Neutral Tooling Core
MiniBus SHALL provide a provider-neutral tooling core that defines read models, query filters, timeline fragments, and explicit action contracts for local operational tooling.

#### Scenario: Shared tooling contracts are available
- **WHEN** a CLI, future API, or future UI needs to inspect MiniBus operational state
- **THEN** it can depend on tooling core contracts for inbox records, outbox records, saga records, timeline fragments, query filters, and action result models without depending on a provider-specific implementation package

#### Scenario: Provider-specific details stay outside the core
- **WHEN** a provider implements tooling readers or actions
- **THEN** provider-specific connection handling, query construction, schema names, table names, and backend SDK usage remain outside the provider-neutral tooling core

### Requirement: SQL Tooling Readers
MiniBus SHALL provide SQL-backed tooling readers for inbox, outbox, and saga state using the existing MiniBus SQL persistence schema.

#### Scenario: SQL inbox records are listed
- **WHEN** a caller requests inbox records from the SQL tooling provider
- **THEN** MiniBus returns records including endpoint name, message id, processed timestamp, correlation id when available, and summary header metadata

#### Scenario: SQL outbox records are listed
- **WHEN** a caller requests outbox records from the SQL tooling provider
- **THEN** MiniBus returns records including operation id, outgoing message id, endpoint name, incoming message id, operation kind, message type, due time, created timestamp, claim timestamp, dispatch timestamp, attempt count, last error summary, and derived dispatch status

#### Scenario: SQL saga records are listed
- **WHEN** a caller requests saga records from the SQL tooling provider
- **THEN** MiniBus returns records including saga id, saga data type, correlation id, created timestamp, updated timestamp, completion state, completion timestamp when available, and version metadata

### Requirement: Operational Query Filters
MiniBus tooling SHALL support common operational filters over SQL-backed inbox, outbox, and saga readers where the underlying state can answer the filter.

#### Scenario: Filter by endpoint and message id
- **WHEN** a caller filters operational records by endpoint name and message id
- **THEN** SQL-backed tooling readers return only matching records from data sets that expose those fields

#### Scenario: Filter by correlation id
- **WHEN** a caller filters operational records by correlation id
- **THEN** SQL-backed tooling readers return matching inbox and saga records and any matching outbox records whose stored headers or incoming message metadata expose that correlation

#### Scenario: Filter by status and time window
- **WHEN** a caller filters outbox or saga records by status and a time window
- **THEN** SQL-backed tooling readers return only records matching the requested state and timestamp bounds where those fields exist

#### Scenario: Unsupported filters fail clearly
- **WHEN** a caller requests a filter that a provider or record type cannot support
- **THEN** MiniBus returns a clear unsupported-filter result instead of silently ignoring the filter

### Requirement: SQL Message Timeline
MiniBus tooling SHALL provide a best-effort message or correlation timeline assembled from available SQL-backed inbox, outbox, and saga state.

#### Scenario: Timeline by message id
- **WHEN** a caller requests a timeline for a message id
- **THEN** MiniBus returns timeline fragments for matching inbox records, outbox records, and related saga records that can be associated through available SQL state

#### Scenario: Timeline by correlation id
- **WHEN** a caller requests a timeline for a correlation id
- **THEN** MiniBus returns timeline fragments for matching inbox records, outbox operations, and saga records ordered by available timestamps

#### Scenario: Missing providers are explicit
- **WHEN** broker, log, trace, audit, or UI providers are not configured
- **THEN** the timeline identifies those sources as unavailable instead of inferring their state from SQL data

### Requirement: CLI Local Troubleshooting Surface
MiniBus SHALL provide a CLI front door for local troubleshooting that uses the shared tooling core and SQL provider.

#### Scenario: List operational records
- **WHEN** a developer invokes CLI commands to list inbox, outbox, or saga records
- **THEN** the CLI queries the configured tooling provider and renders compact human-readable output with useful identifiers, timestamps, status, and correlation fields

#### Scenario: Show message or correlation details
- **WHEN** a developer invokes a CLI command to show details for a message id or correlation id
- **THEN** the CLI renders the best-effort SQL timeline and available record details without requiring a UI or live Azure Service Bus namespace

#### Scenario: Machine-readable output
- **WHEN** a developer requests machine-readable CLI output
- **THEN** the CLI emits structured output that represents the same core read models and action results used by human-readable commands

### Requirement: Bounded SQL Outbox Drain Action
MiniBus tooling SHALL provide an explicit bounded SQL outbox drain action that reuses the existing `SqlMiniBusOutboxDispatcher`.

#### Scenario: Bounded drain is invoked
- **WHEN** a developer invokes the CLI outbox drain command with explicit bounds
- **THEN** MiniBus resolves or composes the existing SQL outbox dispatcher, drains no more than the requested bounds, and reports the dispatched operation count

#### Scenario: Existing dispatch semantics are preserved
- **WHEN** the tooling drain action dispatches pending SQL outbox operations
- **THEN** MiniBus uses the existing SQL outbox claim, lease, retry metadata, deterministic outgoing message id, and at-least-once dispatch behavior

#### Scenario: Unbounded background drain is not started
- **WHEN** the tooling CLI is used
- **THEN** MiniBus performs only the explicitly requested bounded action and does not start a background dispatcher or message-processing runtime

### Requirement: Tooling Safety Boundaries
MiniBus tooling SHALL document and enforce first-slice safety boundaries for read-only inspection, explicit actions, credentials, and sensitive data.

#### Scenario: Read-only commands do not mutate state
- **WHEN** a developer runs CLI commands that list records or show timelines
- **THEN** MiniBus does not modify inbox, outbox, saga, broker, or runtime state

#### Scenario: Explicit actions are distinguishable
- **WHEN** a developer runs a command that can mutate state, such as bounded outbox drain
- **THEN** the command is clearly modeled and documented as an explicit action rather than read-only inspection

#### Scenario: Sensitive payloads are not dumped by default
- **WHEN** a developer lists or shows operational records
- **THEN** MiniBus does not dump full message bodies, full saga data, or credentials by default

#### Scenario: Deferred unsafe operations are excluded
- **WHEN** a developer uses the first tooling increment
- **THEN** MiniBus does not provide DLQ resubmission, broker message replay, destructive broker operations, arbitrary console log scraping, or live Azure monitor querying

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