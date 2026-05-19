## Context

MiniBus already supports Azure Functions isolated worker integration through manual Service Bus trigger wrapper classes. Those wrappers are intentionally thin: Azure Functions owns the static trigger declaration, MiniBus owns processing through `MiniBusProcessor`, and handlers stay independent of Azure SDK and Functions types.

That design is stable, but the wrapper class is repeated boilerplate for every input queue or topic subscription. Source generation can remove that repetition without changing the runtime processing model.

The current package layout keeps runtime concerns under `src/` and tests under `tests/`. The source generator should follow that shape as a separate distributable package so applications can opt into generated wrappers without changing the existing runtime package behavior.

## Goals / Non-Goals

**Goals:**

- Generate thin Azure Functions isolated worker Service Bus trigger wrappers that delegate to `MiniBusProcessor`.
- Preserve manual wrappers as a supported, documented path.
- Support queue triggers and topic/subscription triggers.
- Keep the declaration API small, explicit, and deterministic.
- Report invalid wrapper declarations as focused compile-time diagnostics.
- Keep generated code inspectable and stable enough for snapshot/source-shape tests.
- Keep Roslyn generator dependencies out of MiniBus runtime packages.

**Non-Goals:**

- Provision Azure Service Bus infrastructure.
- Infer transport routes, queue names, topics, subscriptions, or filters from handlers.
- Generate handlers, message contracts, sagas, or dispatcher code.
- Replace or bypass `MiniBusProcessor`.
- Add broad Roslyn analyzers beyond diagnostics needed by the generator.
- Add project templates in this change.
- Require existing applications to adopt source generation.

## Decisions

### 1. Add a separate source generator package

Create a new `MiniBus.AzureFunctions.SourceGenerators` package with generator and diagnostic logic. The package should be referenced by consuming applications as an analyzer/source-generator package, while runtime processing remains in `MiniBus.AzureFunctions`.

Rationale: source generation is developer tooling, not runtime processing. Keeping it separate prevents Roslyn dependencies and analyzer packaging conventions from leaking into the existing adapter package.

Alternative considered: put the generator in `MiniBus.AzureFunctions`. That would make discovery simple, but it couples runtime and compile-time dependencies and makes package behavior harder to reason about.

### 2. Use assembly-level declaration attributes for v1

Applications declare generated wrappers with MiniBus-owned assembly-level attributes, for example:

```csharp
[assembly: MiniBusSourceGeneratedServiceBusQueueFunction(
    functionName: "BillingInput",
    queueName: "billing-queue",
    connection: "ServiceBus")]

[assembly: MiniBusSourceGeneratedServiceBusTopicFunction(
    functionName: "BillingEvents",
    topicName: "domain-events",
    subscriptionName: "billing",
    connection: "ServiceBus")]
```

The source generator can provide these declaration attribute types through post-initialization source so the consuming app does not need a runtime marker assembly. The attributes live in the reserved `MiniBus.AzureFunctions.SourceGenerators.Declarations` namespace and use source-generator-prefixed type names to avoid colliding with future runtime APIs.

Rationale: wrapper declarations are endpoint-level input declarations rather than message-type behavior. Assembly attributes keep the call site small, avoid empty partial classes, and make the generated output easy to map one-to-one with trigger inputs.

Alternative considered: partial marker classes. They allow more customization later but require users to write nearly as much ceremony as the generated wrapper would remove.

### 3. Generate one wrapper class per declaration

Each declaration generates one public sealed class in the reserved `MiniBus.AzureFunctions.__Generated` namespace. The class constructor accepts `MiniBusProcessor`, stores it, and exposes one function method with Azure Functions isolated worker attributes.

Queue declarations generate a method equivalent to:

```csharp
[Function("BillingInput")]
public Task Run(
    [ServiceBusTrigger("billing-queue", Connection = "ServiceBus")]
    ServiceBusReceivedMessage message,
    ServiceBusMessageActions actions,
    CancellationToken cancellationToken)
{
    return _processor.ProcessAsync(message, actions, cancellationToken);
}
```

Topic declarations use `ServiceBusTrigger(topicName, subscriptionName, Connection = connection)`.

Rationale: generated code should mirror the supported manual pattern exactly. This keeps behavior understandable and makes rollback trivial: copy the generated shape into a manual wrapper if needed.

Alternative considered: generate a shared base class or helper. That adds indirection without reducing generated complexity meaningfully.

### 4. Keep generated wrappers in the consuming app assembly

Generated source is added to the consuming application compilation. The source generator package does not produce a runtime assembly that contains wrappers.

Rationale: Azure Functions discovers trigger functions from the function app assembly. Generating into the app assembly keeps deployment and discovery aligned with normal Functions behavior.

Alternative considered: emit wrappers from a separate build output. That would be harder to integrate with Functions discovery and local debugging.

### 5. Require explicit trigger metadata

Each declaration must include a non-empty function name, connection setting name, and queue name or topic/subscription names. Function names must be unique within the generated declarations. Generated type names are derived from function names unless an optional explicit type name is added during implementation.

Rationale: MiniBus should not infer infrastructure naming in v1. Explicit declarations are predictable and make diagnostics straightforward.

Alternative considered: infer function names or connection names from endpoint options. That would couple compile-time generation to runtime DI configuration and make generated behavior surprising.

### 6. Emit focused generator diagnostics

The generator should report diagnostics for empty names, duplicate function names, invalid generated type names when explicit type naming is supported, and declarations that cannot be represented as Azure Functions triggers. Diagnostics should point at the declaration syntax when possible and use stable diagnostic IDs.

Rationale: source generators fail best when they explain user-declaration problems at compile time. This also lays groundwork for broader analyzers later without trying to solve analyzer coverage now.

Alternative considered: silently skip invalid declarations. That would make missing Functions hard to diagnose.

### 7. Test by compiling sample inputs

Tests should use Roslyn compilation helpers to feed sample declarations into the generator and assert generated source shape and diagnostics. Tests should cover queue, topic/subscription, duplicate names, empty names, and the absence of generated wrappers when declarations are invalid.

Rationale: generator behavior is primarily compile-time source output, so tests should verify the generated code contract rather than only unit-test private helper methods.

Alternative considered: only build the sample project. That catches integration errors but gives weak coverage for diagnostics and source shape.

## Risks / Trade-offs

- [Risk] Generated functions might not be discovered by Azure Functions if generated visibility, namespace, or attribute usage is wrong. → Mitigation: mirror the existing manual wrapper pattern and add a buildable sample/reference test that compiles generated wrappers with Functions attributes.
- [Risk] Declaration attributes generated by the generator could conflict with future runtime APIs. → Mitigation: choose a clearly MiniBus-owned namespace and treat attribute names as public API.
- [Risk] Assembly-level attributes may feel less discoverable than class-level declarations. → Mitigation: document the pattern near the existing manual wrapper documentation and keep examples short.
- [Risk] Source generator packaging can accidentally leak Roslyn dependencies into runtime packages. → Mitigation: isolate the generator project and verify package output metadata/readme separately.
- [Risk] v1 explicit metadata may feel repetitive for many triggers. → Mitigation: prefer predictable v1 behavior and leave templates or higher-level conventions for later changes.

## Migration Plan

No migration is required. Existing manual wrappers continue to compile and remain supported. Applications can opt into the generator one wrapper at a time by adding the generator package and assembly-level declarations. Rollback means removing the generator package/declarations and keeping or restoring manual wrapper classes.

## Open Questions

- Should v1 support an optional generated class name override, or should type names always derive from function names?
- Should the generator require a connection setting name, or allow Azure Functions defaults when the connection argument is omitted?
- Resolved: declaration attributes live in `MiniBus.AzureFunctions.SourceGenerators.Declarations` to signal compile-time-only source generator ownership and reduce runtime API collision risk.
