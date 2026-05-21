## Context

MiniBus already has the runtime pieces needed for an Azure Functions + Azure Service Bus application and documents the manual golden path in `README.md`. `samples/MiniBus.Samples.FunctionApp` demonstrates the recommended registration hook, a thin manual Service Bus trigger wrapper, Azure Service Bus route/dispatcher setup, recoverability configuration, handler-facing code, optional source-generated wrappers, and saga examples. The sample intentionally remains only Functions-oriented buildable code instead of a full host executable until a reusable host template exists.

The first template slice should turn that stable path into a starter project without pulling the richer billing sample, SQL deployment concerns, Azure resource provisioning, or a broad matrix of template options into the first implementation. It must also work with the current distribution posture: MiniBus packages are locally pack-verifiable today even though repository automation does not publish them to NuGet.

## Goals / Non-Goals

**Goals:**
- Add one reusable `dotnet new` template for an Azure Functions isolated-worker MiniBus starter project using Azure Service Bus.
- Generate a complete host project that builds without live Azure resources and shows the smallest coherent message-processing path.
- Encode the current golden-path defaults: message contracts, one handler, MiniBus registration, Azure Service Bus transport registration, one thin trigger wrapper, recoverability settings, and local guidance.
- Make template installation, invocation, package production, and generated-project build verification explicit.
- Keep the output easy to extend with SQL persistence, source-generated wrappers, additional subscriptions, sagas, and deployment choices after generation.

**Non-Goals:**
- Expanding the billing sample into a full reference application or generating a billing clone.
- Shipping a family of v1 options for wrappers, sagas, persistence, transports, or multiple endpoint topologies.
- Provisioning Azure Service Bus entities, credentials, Functions infrastructure, SQL schema, or deployment pipelines.
- Adding live Azure Service Bus integration tests for the template path.
- Introducing public runtime APIs solely to make template output look different from the existing supported setup path.

## Decisions

### Generate a full isolated-worker host in v1

The template SHALL generate the runnable Azure Functions application shape that the current sample deliberately avoids owning: a host entry point, Functions host metadata, application code, and local settings placeholders. The generated project should build immediately after template instantiation and should be ready to run once the developer supplies application-owned Azure Functions and Service Bus configuration.

Alternative considered: keep the template as a library-shaped registration sample. That would duplicate the current sample limitation and would not remove the most awkward starter work from the golden path.

### Ship one focused starter template

The first short name should be a single focused template such as `minibus-functionapp` inside a template package such as `MiniBus.Templates`. The default output should represent one endpoint with one inbound queue wrapper and starter routing for the generated messages. Normal template-engine project naming remains useful, but v1 should not expose switches for wrapper style, persistence, saga inclusion, sample domains, or topology breadth.

Alternative considered: start with a template family or many v1 flags. That increases verification branches before MiniBus has one proven generated project path and makes the starter experience less legible.

### Use package references and verify against a local package feed

Generated application projects should consume MiniBus package references, not source-tree project references. Repository verification should pack the runtime packages needed by the template, pack or install the template artifact, instantiate the template into a scratch directory, and build the generated project against the local package output. Documentation can show the eventual package-install path and the repository-local contributor path while NuGet publishing remains outside this change.

Alternative considered: use project references when the template is tested from this repository. That would make verification easier but would produce the wrong shape for real consumers.

### Keep manual wrappers as the default trigger surface

The generated starter should include a thin manual Service Bus queue-trigger wrapper that injects `MiniBusProcessor` and delegates to `ProcessAsync`, matching the clearest sample and README path. Template-local docs should explain that `MiniBus.AzureFunctions.SourceGenerators` can remove wrapper boilerplate later, but the generated project should not depend on generated wrappers in v1.

Alternative considered: generate source-generator declarations by default. The generator is useful, but manual wrapper source is easier for a first-time developer to inspect, debug, and modify before they know MiniBus conventions.

### Generate a small message path rather than a saga workflow

The output should contain starter command/event contracts and one handler that processes the inbound command through MiniBus APIs and performs a small outgoing operation such as publishing the starter event. Routes for the generated outgoing message belong next to the transport registration so the handler has a coherent path. The template should not generate saga data, timeout messages, saga registration, or SQL saga persistence.

Alternative considered: include the sample saga because it demonstrates long-running workflows. That would force scheduled routes, saga persistence choices, and more concepts into the first generated project than the starter needs.

### Keep Service Bus placeholders explicit and application-owned

The template should generate configuration placeholders for the Azure Functions Service Bus connection setting name used by the trigger wrapper, the inbound queue name, and any starter outbound route destinations. A local settings file or sample local settings file may carry a non-secret `ServiceBus` placeholder alongside normal Functions development settings. The generated README must explain that namespace provisioning, connection-string or identity configuration, queue/topic/subscription creation, route naming, retry observability, and production deployment remain application-owned.

Alternative considered: hide topology and connection choices behind framework defaults. That conflicts with the current explicit routing model and would imply Azure provisioning behavior this change intentionally does not provide.

### Omit SQL persistence from generated output but document the extension point

SQL inbox/outbox/saga persistence should be documented as the next production reliability step rather than generated by default or hidden behind a v1 option. The generated project can point developers back to repository/package guidance for schema scripts, persistence registration, and outbox dispatch ownership.

Alternative considered: include SQL registration placeholders. Empty SQL persistence wiring makes the generated project look more production-complete than it is and adds database deployment decisions before the starter path is understood.

### Include analyzers by default and keep source generators opt-in

The starter should reference `MiniBus.Analyzers` with analyzer-style private assets so new applications get compile-time guidance for the exact configuration and handler mistakes templates are meant to reduce. The source-generator package remains an explicitly documented follow-up because it changes the trigger wrapper authoring style rather than improving the runtime starter path.

Alternative considered: make both analyzer and source-generator packages opt-in. That keeps dependencies minimal, but the analyzers are specifically useful in a generated starter and do not affect runtime behavior.

### Verification centers on packaging and generated-project buildability

Automated coverage should prove that template metadata is installable or discoverable by `dotnet new`, that a generated project contains the intended starter files/configuration, and that at least the default generated project builds against locally packed MiniBus dependencies without live Azure Service Bus. The broader solution build and package checks should continue to cover the underlying packages.

Alternative considered: add live Azure Service Bus template acceptance coverage. That tests environment provisioning and credentials more than template correctness and belongs after reusable infrastructure exists.

## Risks / Trade-offs

- [Risk] A generated host may imply a production-ready deployment story. -> Mitigate with local README guidance that separates buildable starter output from Azure provisioning, security, topology, and deployment ownership.
- [Risk] Package-reference verification is more complex before automated NuGet publishing exists. -> Mitigate with a local package feed in template verification so the consumer shape stays accurate.
- [Risk] Manual wrappers add boilerplate compared with generated wrappers. -> Mitigate by keeping the wrapper intentionally thin and documenting the source-generator follow-up.
- [Risk] Omitting SQL persistence can understate production reliability needs. -> Mitigate by documenting SQL as an explicit next step and keeping repository golden-path guidance visible from the generated project.
- [Risk] Default analyzer diagnostics may surface guidance before a developer has changed the starter. -> Mitigate by ensuring generated output itself satisfies analyzer expectations and by keeping the dependency analyzer-only.

## Migration Plan

1. Add the template package and the single default template with the generated host files and local documentation.
2. Add verification that packs dependencies locally, installs or discovers the template, instantiates it, and builds the generated project.
3. Update root documentation and the active backlog so developers can enter the golden path through the new template.
4. Preserve the existing manual README/sample path for developers who prefer assembling or studying the setup manually.

Rollback is straightforward: remove the template package/assets and documentation references while retaining the existing sample and manual golden path.

## Open Questions

No product-scope questions remain open for v1. The exact starter domain names and contributor command plumbing can be finalized during implementation as long as they preserve the generated shape, defaults, and verification contract described here.
