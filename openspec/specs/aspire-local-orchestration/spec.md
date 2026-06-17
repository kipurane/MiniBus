## Purpose

Define the local Aspire orchestration experience for running the MiniBus reference workflow, supporting infrastructure, SQL reliability path, and tooling web app as one development system.

## Requirements

### Requirement: Local Aspire AppHost
MiniBus SHALL provide an Aspire AppHost for the local reference environment that composes existing sample and tooling projects without making Aspire a runtime dependency of MiniBus packages.

#### Scenario: AppHost composes reference projects
- **WHEN** a developer opens or runs the local Aspire AppHost
- **THEN** the AppHost includes orchestration entries for the Billing Function App, Inventory Function App, Billing SQL outbox dispatcher Function App, and `MiniBus.Tooling.Web`

#### Scenario: Aspire dependencies stay isolated
- **WHEN** MiniBus runtime, transport, persistence, tooling, and sample Function App packages are built
- **THEN** those packages do not gain Aspire hosting dependencies except through the dedicated AppHost project

### Requirement: Local Infrastructure Composition
The Aspire AppHost SHALL coordinate the local infrastructure needed by the reference workflow, including SQL Server, Azure Service Bus emulator support, and Functions storage.

#### Scenario: Shared local infrastructure is configured
- **WHEN** the AppHost configures the reference environment
- **THEN** it provides consistent Service Bus, Billing SQL, Billing SQL schema, and Functions storage settings to the projects that require them

#### Scenario: Existing Service Bus emulator topology is preserved
- **WHEN** the AppHost starts or documents Azure Service Bus emulator support
- **THEN** it uses the existing sample topology names for `billing-queue`, `inventory-queue`, `billing-receipts`, `domain-events`, `billing`, and `billing-timeouts`

#### Scenario: Manual infrastructure path remains supported
- **WHEN** a developer chooses not to use Aspire
- **THEN** the existing compose and script-based sample workflow remains documented and supported

### Requirement: Orchestrated SQL Reliability Workflow
The Aspire local environment SHALL support the SQL-backed Billing reliability workflow with visible schema setup and separate outbox dispatch ownership.

#### Scenario: Billing uses SQL persistence settings
- **WHEN** the orchestrated Billing Function App runs with SQL persistence enabled
- **THEN** it receives the Billing SQL connection and schema settings used for inbox, outbox, and saga persistence

#### Scenario: Dispatcher uses same SQL and Service Bus settings
- **WHEN** the orchestrated Billing SQL outbox dispatcher Function App runs
- **THEN** it uses the same Billing SQL, schema, Service Bus, schedule, and drain-bound settings as the Billing reference workflow

#### Scenario: Schema setup is explicit
- **WHEN** a developer follows the Aspire local workflow
- **THEN** the documentation or AppHost flow makes SQL schema application an explicit local setup step instead of silently applying schema to arbitrary SQL targets

### Requirement: Tooling Web Integration
The Aspire local environment SHALL connect `MiniBus.Tooling.Web` to the same SQL state produced by the orchestrated Billing workflow.

#### Scenario: Tooling reads orchestrated SQL state
- **WHEN** `MiniBus.Tooling.Web` runs under the AppHost
- **THEN** it is configured with `MiniBus:Tooling:Sql:ConnectionString` and `MiniBus:Tooling:Sql:SchemaName` values that target the orchestrated Billing SQL database

#### Scenario: Tooling remains read-only
- **WHEN** a developer uses `MiniBus.Tooling.Web` in the Aspire local environment
- **THEN** the web API and UI remain read-only and do not expose mutating actions such as outbox drain, retry, DLQ resubmission, or message replay

### Requirement: Documentation And Verification
MiniBus SHALL document and verify the Aspire local orchestration path.

#### Scenario: Documentation explains local orchestration
- **WHEN** a contributor reads the sample or repository documentation
- **THEN** it explains prerequisites, license acceptance, how to run the AppHost, how SQL schema setup works, how to seed the Billing workflow, and how the Aspire path relates to existing scripts

#### Scenario: AppHost build is verified
- **WHEN** the repository verification for the Aspire change runs
- **THEN** it builds the AppHost and verifies the orchestrated project references and required configuration keys without requiring live Azure resources
