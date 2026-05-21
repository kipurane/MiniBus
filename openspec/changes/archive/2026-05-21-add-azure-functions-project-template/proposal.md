## Why

MiniBus now has a documented Azure Functions golden path and a buildable Function App sample, but a developer still has to assemble the recommended registration, trigger-wrapper, transport, and configuration shape by hand. Project templates are the next developer-experience step because the active backlog calls for them and the existing sample deliberately stops short of owning the reusable isolated-worker host shape a starter project should provide.

## What Changes

- Add the first reusable `dotnet new` project-template slice for a MiniBus Azure Functions isolated-worker application backed by Azure Service Bus.
- Keep v1 focused on one opinionated starter project with manual Service Bus trigger wrappers, starter message contracts, one handler, MiniBus and transport registration, recoverability defaults, configuration placeholders, and generated-project documentation.
- Generate a complete isolated-worker host project that can build after template creation while leaving Azure Service Bus namespace provisioning, credentials, topology ownership, and production deployment choices to the application.
- Keep SQL inbox/outbox/saga persistence and a saga workflow outside the default template output; document where they fit and leave richer template options to follow-up work.
- Define how the template is packaged, installed, invoked, and verified, including generated-project build coverage.
- Update developer-facing documentation and backlog guidance so the golden path can point at the template while still describing optional source generators, analyzers, and deferred infrastructure automation accurately.

## Capabilities

### New Capabilities
- `azure-functions-project-template`: Defines the v1 `dotnet new` Azure Functions + Azure Service Bus starter project shape, defaults, documentation, packaging, invocation, and generated-project verification expectations.

### Modified Capabilities
- `package-readiness`: Updates root developer guidance now that a project template exists instead of being described only as future work.

## Impact

- Adds template assets and template packaging metadata to the repository and normal local verification flow.
- Adds tests or build verification that instantiate the template and build the generated project without live Azure resources.
- Updates root documentation, template-local documentation, and the active OpenSpec backlog entry for project templates.
- Reuses existing MiniBus runtime APIs, Azure Functions adapter APIs, Azure Service Bus transport APIs, analyzer/source-generator packaging conventions, and the Function App sample as references; no public runtime API expansion is expected unless implementation exposes a concrete blocker.
