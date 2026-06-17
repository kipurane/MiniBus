## Why

MiniBus has the runtime packages, SQL reliability pieces, emulator-backed reference samples, dispatcher sample, and SQL-backed tooling needed for a realistic local system, but contributors still have to start and coordinate those pieces manually. Adding an Aspire AppHost now turns the existing reference workflow into one coherent development experience while keeping Aspire outside MiniBus runtime packages.

## What Changes

- Add a local Aspire orchestration project for the MiniBus reference environment.
- Orchestrate SQL Server, the Azure Service Bus emulator, the Billing Function App, the Inventory Function App, the Billing SQL outbox dispatcher Function App, and `MiniBus.Tooling.Web`.
- Share connection strings and environment variables consistently across the orchestrated services.
- Preserve existing manual scripts and compose assets as supported local paths.
- Document how to run the Aspire environment, where SQL schema setup fits, and how the orchestrated flow relates to the existing sample scripts.
- Add focused verification that the AppHost builds and that orchestration wiring remains coherent.

## Capabilities

### New Capabilities

- `aspire-local-orchestration`: Defines the local Aspire-based orchestration contract for the MiniBus reference environment.

### Modified Capabilities

- None.

## Impact

- Adds one sample/development AppHost project and related project references.
- Adds Aspire hosting dependencies only to the AppHost, not to MiniBus runtime packages.
- Affects sample documentation, repository-level development guidance, and OpenSpec backlog status.
- May touch sample configuration to make existing SQL, Service Bus emulator, Functions, dispatcher, and tooling settings easier to compose from one orchestrator.
