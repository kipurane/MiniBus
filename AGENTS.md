# Repository Guidelines

## Project Structure

`src/` contains the .NET packages, including transport-independent `MiniBus.Core`, Azure Functions and Service Bus adapters, SQL and Azure Storage persistence, analyzers, templates, and operational tooling. The tooling web client lives in `src/MiniBus.Tooling.Web/ClientApp/`. Matching test projects live under `tests/`. Runnable Billing, Inventory, and Aspire examples are in `samples/`; documentation is in `docs/` and package READMEs. OpenSpec requirements and changes live under `openspec/`.

## Build and Development

- `dotnet restore MiniBus.sln` restores solution dependencies.
- `dotnet build MiniBus.sln` builds the solution using the SDK pinned in `global.json`.
- `dotnet test MiniBus.sln` runs the solution tests; infrastructure-dependent tests require additional setup.
- `./eng/verify-templates.sh` verifies the project templates.
- `ACCEPT_EULA=Y ASPIRE_CONTAINER_RUNTIME=podman dotnet run --project samples/MiniBus.Samples.AppHost/MiniBus.Samples.AppHost.csproj` starts the local reference environment. See the sample README for prerequisites.

On macOS, configure Podman for the SQL Server and Azurite Testcontainers tests as described in the root README. Start the Service Bus emulator separately before running its acceptance tests.

## Coding Style and Architecture

Follow existing C# conventions: four-space indentation, PascalCase for public types and members, and camelCase for parameters and local variables. Keep nullable annotations enabled and prefer asynchronous APIs with cancellation tokens where existing interfaces support them. Keep business handlers and `MiniBus.Core` independent of Azure SDK and Functions trigger types. Follow the existing React and TypeScript conventions in the tooling client.

## Testing

Tests use xUnit and are organized by package. Name test classes after the component and test methods after the behavior and expected outcome, such as `Resolve_RejectsUnknownIdentity`. Add focused regression tests for changed behavior. Run a single project with `dotnet test tests/MiniBus.Core.Tests/MiniBus.Core.Tests.csproj`; run the solution suite for cross-package changes. Live Azure Service Bus tests require explicit opt-in and a dedicated namespace. See the test READMEs for infrastructure configuration.

## Commits and Pull Requests

Recent commits use short imperative subjects, sometimes with prefixes such as `feat:`, `docs:`, or `chore:`. Keep each commit focused. In pull requests, describe the behavior and motivation, link relevant issues or OpenSpec changes, and report the tests run and any skipped infrastructure checks. Include screenshots for visible tooling UI changes.

## Configuration and OpenSpec

Keep credentials out of source control and test output. Use environment variables or local settings for connection strings. Check `openspec list` and `openspec status` when working on a change, and keep its task checklist aligned with completed work.
