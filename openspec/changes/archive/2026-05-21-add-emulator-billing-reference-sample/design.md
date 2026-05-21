## Context

`samples/MiniBus.Samples.FunctionApp` currently demonstrates the Billing endpoint shape with manual Azure Functions wrappers, MiniBus registration, routes, one command handler, and a saga timeout path. It intentionally remains a buildable reference project rather than a runnable host: `Program` only exposes the registration hook and the registered `SampleServiceBusSender` throws if outgoing transport dispatch is attempted.

The remaining developer-experience backlog now prefers a local Azure Service Bus emulator workflow before adding Inventory or a multi-endpoint sample and before treating live Azure Service Bus as the proof layer. This change therefore crosses sample code, Functions hosting setup, Service Bus configuration, local workflow docs, and verification. It should keep the sample faithful to MiniBus architecture: Azure Functions wrappers stay thin, handlers stay transport independent, and outgoing work continues through `MiniBusContext` and transport routing.

## Goals / Non-Goals

**Goals:**

- Make the existing Billing sample locally runnable against the Azure Service Bus emulator without requiring a real Azure subscription or Service Bus namespace.
- Own the local Billing topology assets needed by the sample, including its queue, topic/subscription, and scheduled-message destination shape.
- Use the real Azure Service Bus sender and connection configuration for local outgoing `Send`, `Publish`, and scheduling work instead of the throwing placeholder transport path.
- Provide a repository-owned way to submit the first Billing command and a clear workflow for observing the local reference path.
- Verify the emulator-backed workflow at an appropriate local level while keeping live Azure Service Bus coverage separate.

**Non-Goals:**

- Expand the reference app to Inventory, multiple endpoints, or a broader business workflow.
- Introduce general Azure infrastructure provisioning or deployment automation.
- Make live Azure Service Bus integration tests part of this change.
- Make SQL persistence, outbox draining, or production reliability wiring mandatory for the local sample path.
- Promise emulator behavior that has not been validated for the Functions-trigger and scheduled-timeout path.

## Decisions

### Make the Billing sample itself the runnable Functions host

Promote `samples/MiniBus.Samples.FunctionApp` from a registration-hook project into the complete local host path needed to run Azure Functions against the emulator. The existing registration extension, wrappers, handlers, saga, and routes should remain legible pieces inside that host rather than moving the runnable behavior into a second shadow sample.

Alternative considered: keep the current sample build-only and add run guidance around a separate temporary host or the project template. That would preserve the current project boundary, but it would make the reference workflow harder to discover and would split the Billing story across two samples.

### Prefer repo-owned emulator topology over a real Azure namespace

Add local emulator configuration and supporting instructions for the Billing entities the sample depends on: the Billing command queue, receipt destination, domain event topic plus Billing subscription, and the timeout destination registered by the saga route. The default run path should use emulator connection settings so a developer can repeat the workflow locally with Docker-backed infrastructure.

Alternative considered: document real Azure Service Bus provisioning only. That would prove the cloud shape earlier, but it raises the cost of the primary sample loop and conflicts with the backlog decision to establish the emulator-backed workflow first.

### Use the normal Service Bus transport path for local dispatch

The emulator run path should register the real `ServiceBusClient` and `AzureServiceBusSender` wiring used by MiniBus transport dispatch. Handler code should not learn about emulator details, and transport routes should remain the place that maps `CreateInvoice`, `SendInvoiceReceipt`, `InvoiceCreated`, and timeout work to Service Bus destinations.

Alternative considered: keep `SampleServiceBusSender` and simulate outgoing operations for the emulator workflow. That would only prove receive-side Functions processing and would leave the main local reference gap in place.

### Seed the workflow through a repo-owned Billing command path

Provide a local way to submit a `CreateInvoice` message to the emulator with the MiniBus metadata required by the receive pipeline. The seeding path should be easy to run from the repository and should avoid depending on the Azure portal or an external Service Bus exploration tool for the primary sample story.

Alternative considered: require developers to craft raw Service Bus messages manually. That obscures the message-type and header conventions that are part of the framework path being demonstrated.

### Define the first observable workflow before expanding it

The local workflow should prove the Billing command input path, handler dispatch of the receipt command and `InvoiceCreated` event, and Billing event subscription processing through the thin Azure Functions wrappers. The saga timeout route remains part of the Billing topology because the sample already demonstrates timeout scheduling, but user-facing claims about emulator-backed scheduling or delivered timeout processing should follow validation of the emulator plus Azure Functions path.

Alternative considered: require full timeout delivery in the first workflow unconditionally. That would make the sample contract depend on the least-proven emulator behavior and on the current long timeout shape rather than the core Billing path.

### Keep verification local and layered

Keep normal build and repository verification for the sample, then add emulator-backed verification that can validate the documented local workflow when the required local emulator infrastructure is available. Live Azure Service Bus tests remain later validation work rather than a fallback required for this change to be complete.

Alternative considered: cover the new flow only through documentation. That would let the sample drift quickly because host wiring, emulator topology, seed messages, and dispatch configuration must agree for the reference path to work.

## Risks / Trade-offs

- [Risk] Azure Service Bus emulator behavior can differ from the cloud service or from Azure Functions trigger expectations. -> Validate the documented workflow explicitly, describe relevant emulator limits, and keep live Azure coverage as a separate proof layer.
- [Risk] Docker-backed emulator setup adds local dependencies to a sample intended to reduce friction. -> Keep build verification independent from the emulator path and make the local run steps explicit and bounded.
- [Risk] Promoting the sample to a runnable host can make it overlap with the starter template. -> Keep the template a minimal starter and use the Billing sample for the richer reference workflow with emulator assets and observed behavior.
- [Risk] Timeout scheduling may be harder to observe than command and event processing. -> Keep timeout claims validation-gated and avoid making timeout delivery the only proof that the local workflow works.

## Migration Plan

1. Preserve the Billing sample location and its handler-facing APIs while extending its project shape into a runnable Functions host.
2. Replace the local placeholder sender path with configuration-backed Service Bus sender registration for emulator use.
3. Add emulator topology and seeding/run guidance alongside the sample so the new path is discoverable.
4. Keep live Azure setup out of the default workflow; later changes can add cloud smoke coverage from the same stable Billing story.

## Open Questions

- Does the chosen emulator-backed Azure Functions path reliably cover the topic/subscription wrapper and scheduled-timeout dispatch path, or should the first observed workflow stop at event subscription processing and document timeout scheduling as unverified locally?
- Should the seed command live as a small sample executable, a sample-owned command mode, or another repository-local helper that best matches existing .NET conventions in this repo?
