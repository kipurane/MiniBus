## Context

MiniBus now has a transport-agnostic core layer for message contracts, serialization, context, and handler invocation, plus an Azure Service Bus transport package for outbound send/publish/schedule and Service Bus header mapping. The missing inbound host integration is Azure Functions isolated worker: Service Bus trigger messages need to become MiniBus processing work, handlers need a `MiniBusContext`, and outgoing operations requested by handlers need to flow through the existing Service Bus transport.

Azure Functions trigger declarations are static, so MiniBus cannot fully hide the function wrapper at runtime. The adapter should provide a small manual wrapper pattern and centralize all processing, context creation, dispatch, and settlement behavior in `MiniBusProcessor`.

## Goals / Non-Goals

**Goals:**
- Add or update `MiniBus.AzureFunctions` for Azure Functions isolated worker integration.
- Provide `MiniBusProcessor` as the main entry point for Service Bus trigger wrappers.
- Support a processing overload with `ServiceBusReceivedMessage` only and a settlement overload with `ServiceBusReceivedMessage` plus `ServiceBusMessageActions`.
- Convert received Service Bus messages into MiniBus processing input by extracting body, MiniBus headers, message id, correlation id, causation id, and message type metadata.
- Resolve the concrete message type from MiniBus message-type headers.
- Deserialize received message bodies through `IMessageSerializer`.
- Invoke handlers through the existing MiniBus core handler invocation pipeline and dependency injection.
- Create an inbound `MiniBusContext` that exposes current message metadata and dispatches outgoing `Send`, `Publish`, and `Schedule` calls through `MiniBus.AzureServiceBus`.
- Complete the incoming message after successful processing when settlement actions are provided.
- Dead-letter the incoming message after unrecoverable processing failure when settlement actions are provided.
- Provide DI extensions for registering the Azure Functions adapter and required dependencies.
- Add unit tests for processor behavior using mocks, fakes, or Azure SDK model factories where possible.
- Add a minimal sample Azure Function wrapper using `ServiceBusTrigger`.

**Non-Goals:**
- SQL inbox or outbox.
- Immediate retries, delayed retries, or recoverability policy configuration.
- Sagas or saga persistence.
- Source-generated Azure Function wrappers.
- Service Bus session processing.
- Advanced dead-letter diagnostics beyond a basic reason and description.
- Live Azure Service Bus or full Azure Functions host integration tests unless infrastructure already exists.

## Decisions

### 1. Keep Azure Functions classes thin and manual

The supported wrapper pattern remains explicit and small:

```csharp
[Function("BillingInput")]
public Task Run(
    [ServiceBusTrigger("billing-queue", Connection = "ServiceBus")]
    ServiceBusReceivedMessage message,
    ServiceBusMessageActions actions,
    CancellationToken cancellationToken)
{
    return processor.ProcessAsync(message, actions, cancellationToken);
}
```

**Rationale:** Azure Functions bindings are static. Keeping wrapper code visible while delegating immediately to `MiniBusProcessor` fits the platform and keeps host concerns outside handlers.

**Alternatives considered:**
- Generate wrappers now: rejected because source generation is explicitly out of scope.
- Let handlers receive Function or Service Bus trigger types: rejected because handlers must remain transport-independent.

### 2. Provide both settlement and non-settlement processor overloads

`ProcessAsync(ServiceBusReceivedMessage, CancellationToken)` will perform type resolution, deserialization, context creation, handler invocation, and outgoing dispatch, but it will not complete or dead-letter the message. `ProcessAsync(ServiceBusReceivedMessage, ServiceBusMessageActions, CancellationToken)` will wrap the same processing flow with basic complete/dead-letter settlement.

**Rationale:** Some tests or hosting flows may want processing without adapter-owned settlement, while real manual trigger wrappers need explicit settlement through `ServiceBusMessageActions`.

**Alternatives considered:**
- Only expose the settlement overload: rejected because the requested scope includes a received-message-only overload.
- Make the no-settlement overload silently swallow failures: rejected because without settlement actions the caller must be able to observe failures.

### 3. Resolve message type from MiniBus headers

The processor will resolve the message type from `MiniBus.MessageType`, falling back to `MiniBus.EnclosedMessageTypes` when needed. It will fail processing when metadata is missing or cannot resolve to a CLR `Type`.

**Rationale:** The core serializer requires an explicit `Type`, and prior specs established MiniBus message type metadata as the transport-independent contract identity.

**Alternatives considered:**
- Infer type from queue or subscription names: rejected because entity names are routing concerns, not contract identity.
- Require generic function wrappers: rejected because wrappers should stay minimal and non-generic.

### 4. Reuse Service Bus header mapping from `MiniBus.AzureServiceBus`

The adapter will reuse the Service Bus transport’s mapping from application properties to MiniBus headers. It can enrich context metadata from Service Bus system properties when headers are missing.

**Rationale:** Header behavior must be consistent for outbound and inbound Service Bus messages. Duplicating the conversion in the Functions adapter would create drift.

**Alternatives considered:**
- Move Service Bus header mapping into `MiniBus.Core`: rejected because Service Bus application properties are transport-specific.
- Copy mapping into `MiniBus.AzureFunctions`: rejected because it duplicates transport behavior.

### 5. Dispatch outgoing operations through the existing Service Bus transport

The inbound context created by the Functions adapter will implement `Send`, `Publish`, and `Schedule` by delegating to the existing Azure Service Bus transport dispatcher. The context will carry inbound headers forward so outgoing messages can preserve correlation and causation metadata where supported by the transport factory.

**Rationale:** Handlers should use only `MiniBusContext`, while dispatch mechanics remain in `MiniBus.AzureServiceBus`.

**Alternatives considered:**
- Leave outgoing operations unsupported in the adapter: rejected because the revised scope explicitly requires dispatch.
- Build Service Bus messages directly in `MiniBus.AzureFunctions`: rejected because message construction and routing belong to the Service Bus transport package.

### 6. Keep settlement deliberately simple

The settlement overload will complete the incoming message after successful processing. If type resolution, deserialization, handler invocation, or outgoing dispatch fails unrecoverably, it will dead-letter the message with a basic reason and description. No retries, abandon/defer behavior, delayed retries, or rich diagnostics are part of this change.

**Rationale:** The requested settlement behavior is intentionally minimal and leaves recoverability policy for a later change.

**Alternatives considered:**
- Let the Functions host auto-complete: rejected because explicit settlement behavior is part of this adapter.
- Add retry policy now: rejected because retries are out of scope.

### 7. Register through dependency injection

The adapter will provide an extension method for registering `MiniBusProcessor`, adapter options, and any adapter-owned services. It will consume already-registered core serializer/handler invocation services and Azure Service Bus transport dispatcher dependencies, or register minimal defaults where the existing packages expose them.

**Rationale:** Azure Functions isolated worker apps are DI-oriented. A registration extension keeps setup discoverable and reduces manual wiring in sample wrappers.

**Alternatives considered:**
- Require direct constructor wiring in every function app: rejected because it makes wrappers less thin and less repeatable.

## Risks / Trade-offs

- **[Risk] Existing core invocation types may not expose a clean public seam for other packages.** → **Mitigation:** expose only the smallest invocation abstraction needed by the adapter or add a narrow public processor-facing facade.
- **[Risk] Outgoing dispatch may require route configuration that is not yet ergonomic in DI.** → **Mitigation:** keep route configuration explicit and fail fast when outgoing destinations are missing.
- **[Risk] Basic dead-lettering can classify transient failures as unrecoverable.** → **Mitigation:** document this as MVP settlement behavior and leave retries/recoverability for a later change.
- **[Risk] Testing `ServiceBusMessageActions` directly can be difficult.** → **Mitigation:** use small settlement seams and Azure SDK model factory helpers while keeping the public processor overload aligned with Functions types.

## Migration Plan

1. Update or create `MiniBus.AzureFunctions` and its test project.
2. Add required Azure Functions isolated worker and Service Bus dependencies.
3. Implement message adaptation, type resolution, deserialization, inbound context, handler invocation, outgoing dispatch, settlement, and DI registration.
4. Add a minimal sample wrapper using `ServiceBusTrigger`.
5. Add unit tests for success, failure, settlement, header/context metadata, outgoing dispatch delegation, and DI registration where possible.
6. Build the solution and run all MiniBus test projects.

Rollback is low risk before applications consume the package: remove the adapter package, tests, sample wrapper, and OpenSpec artifacts.

## Open Questions

- Should the adapter require a configured Azure Service Bus dispatcher for all handlers, or allow read-only handler processing without outgoing dispatch configured?
- Should `MiniBus.MessageType` always take precedence over `MiniBus.EnclosedMessageTypes` when both are present?
- Should the DI extension own Azure Service Bus transport configuration, or only register Functions adapter services and expect transport services to be registered separately?
