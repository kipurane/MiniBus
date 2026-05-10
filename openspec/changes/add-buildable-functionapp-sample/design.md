## Context

MiniBus currently has sample `.cs` files under `samples/MiniBus.Samples.FunctionApp`, but they are solution items rather than a buildable Azure Functions project. The framework now has enough stable surface area to benefit from a small runnable sample: core contracts, Azure Functions processing, Azure Service Bus transport dispatch, recoverability, basic saga support, and SQL inbox/outbox foundations.

The sample should act as a living API check without becoming a large reference application. It should compile against project references, demonstrate the stable registration path, and avoid depending on features that are still explicitly planned, such as first-class SQL Server/Azure SQL provider packaging.

## Goals / Non-Goals

**Goals:**
- Add a buildable Azure Functions isolated worker sample project.
- Show startup registration for MiniBus Azure Functions processing, message serialization, handler registration, Azure Service Bus routes, and transport dispatcher services.
- Include at least one command handler that uses `MiniBusContext` to publish or send outgoing work.
- Keep the existing manual Service Bus trigger wrapper style visible.
- Keep saga usage either included as a clearly separated optional example or retained as sample code that compiles.
- Document required configuration and how to build or inspect the sample.

**Non-Goals:**
- Live Azure infrastructure provisioning.
- End-to-end execution against a real Service Bus namespace.
- First-class SQL Server/Azure SQL persistence configuration.
- Source-generated Function wrappers.
- A large domain sample with multiple bounded contexts.

## Decisions

### Make the sample a normal project

Create `samples/MiniBus.Samples.FunctionApp/MiniBus.Samples.FunctionApp.csproj` and add it to `MiniBus.sln` as a project instead of only listing loose files. This lets `dotnet build` verify that sample code tracks public APIs.

Alternative considered: keep snippets only and rely on documentation. That is lower maintenance, but it fails to catch API drift.

### Use project references

Reference the local MiniBus projects directly so the sample always reflects the current repository code. NuGet packaging can be demonstrated later once package publishing exists.

Alternative considered: mock or fake the framework setup. That would make the sample easier to compile, but it would not exercise the actual registration path developers need.

### Keep runtime dependencies minimal

The sample should include Azure Functions isolated worker packages and Azure Service Bus SDK dependencies needed to compile the function wrapper, but it should not require live Azure resources for build verification.

Alternative considered: include local emulator or live-resource setup. That is better for integration testing, but it belongs in a later infrastructure-backed sample.

### Treat SQL persistence as optional documentation

The sample can mention where SQL inbox/outbox registration would go, but should not include active SQL persistence setup until first-class SQL Server/Azure SQL support exists.

Alternative considered: wire SQL persistence with a placeholder `DbConnection` factory. That would be misleading because the sample would compile but not be runnable without additional application-specific code.

## Risks / Trade-offs

- Sample may need frequent updates while public APIs are still moving. Mitigation: keep it intentionally small and build it in CI/test workflows where possible.
- Function host packages may add restore/build complexity. Mitigation: pin package references consistently with the rest of the solution and keep the sample project simple.
- Developers may assume the sample is production-ready. Mitigation: document that it is a minimal API and setup sample, not a complete production template.

## Migration Plan

1. Convert `samples/MiniBus.Samples.FunctionApp` into a buildable project.
2. Add or update host startup, handlers, message contracts, routing, and function wrapper files.
3. Add the project to `MiniBus.sln`.
4. Update root/sample documentation.
5. Verify with `dotnet build` or `dotnet test` for the solution.
