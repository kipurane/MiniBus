## Context

MiniBus currently has a transport-agnostic `MiniBus.Core` package with message markers, handler contracts, `MiniBusContext`, command routing behavior, and an `IMessageSerializer` abstraction. The project architecture identifies Azure Service Bus as the primary transport, but handlers must remain independent from Azure SDK types and Azure Functions bindings.

This change introduces the first transport package, `MiniBus.AzureServiceBus`. It should convert MiniBus outgoing operations into `Azure.Messaging.ServiceBus.ServiceBusMessage` instances, send them to queues or topics through a testable sender abstraction, and preserve MiniBus headers in Service Bus application properties for later receive-side adapters.

## Goals / Non-Goals

**Goals:**
- Create a `MiniBus.AzureServiceBus` package that builds on `MiniBus.Core` without introducing transport concerns into business handlers.
- Provide a sender abstraction that can send, publish, and schedule through Azure Service Bus while remaining mockable in unit tests.
- Support command send to queues using explicit command routing.
- Support event publish to topics using explicit event topic routing owned by the transport package.
- Support scheduled send for commands, events, and generic messages where a Service Bus destination can be resolved.
- Provide deterministic header mapping between MiniBus string headers and Service Bus application properties.
- Use `IMessageSerializer` for all outbound message body serialization.
- Centralize Service Bus message creation in a transport-level factory so dispatch operations share serialization and header behavior.
- Add unit tests that validate routing, message construction, header mapping, serialization, and sender calls without requiring a live Service Bus namespace.

**Non-Goals:**
- Azure Functions trigger adapters or receive settlement logic.
- SQL inbox, SQL outbox, delayed retry, dead-lettering, recoverability policies, or saga dispatch integration.
- Source generation or automatic function generation.
- Real end-to-end Service Bus integration tests unless reusable infrastructure already exists in the repository.
- Claim-check/blob payload handling beyond preserving a body and headers in the outgoing message.

## Decisions

### 1. Keep Azure Service Bus APIs isolated in the transport package

`MiniBus.AzureServiceBus` will own all references to `Azure.Messaging.ServiceBus`. Public handler-facing APIs remain in `MiniBus.Core`; the transport package exposes configuration and dispatch abstractions for host setup and tests, not for business handler code.

**Rationale:** The project architecture requires Azure Functions and Service Bus to be thin adapters around MiniBus processing. Keeping SDK types out of handlers preserves testability and prevents transport decisions from shaping message contracts.

**Alternatives considered:**
- Add Service Bus types directly to `MiniBusContext`: rejected because it leaks transport concerns into application handlers.
- Put Service Bus message creation in `MiniBus.Core`: rejected because core must remain transport-agnostic.

### 2. Introduce a narrow sender abstraction over Azure SDK clients

The transport package will define an abstraction for sending immediately and scheduling messages to a named entity. A production implementation can wrap `ServiceBusClient`/`ServiceBusSender`, while tests can mock the abstraction and inspect created `ServiceBusMessage` objects.

**Rationale:** `ServiceBusClient` and `ServiceBusSender` are external infrastructure concerns. A narrow abstraction makes command/event dispatch unit-testable without a live namespace and keeps dispatch logic independent from SDK client lifetime details.

**Alternatives considered:**
- Mock Azure SDK clients directly: rejected because SDK client behavior and construction are not the MiniBus contract.
- Create one sender abstraction per operation (`ICommandSender`, `IEventPublisher`, `IScheduler`): rejected for the MVP because the operations share the same Service Bus mechanics and would duplicate test setup.

### 3. Use explicit Service Bus routing for both commands and events

Command sending will resolve a queue destination from explicit command routing. Event publishing will use transport-owned explicit event-to-topic routing, initially supporting a shared topic pattern such as `domain-events`. Scheduled sends will use the command route for commands, event topic route for events, and an explicit schedule destination option for generic `IMessage` instances.

**Rationale:** The architecture requires commands to go to queues and events to topics. `MiniBus.Core` currently includes command routing behavior only, so the transport package needs a clear event routing model without broadening core more than necessary.

**Alternatives considered:**
- Infer destinations from type names: rejected because MiniBus favors explicit routing and fast failure for missing routes.
- Use one topic per event type by convention: rejected for the MVP because the project recommendation is a shared topic with application property filtering.

### 4. Centralize outbound message construction in a factory

A transport-level factory will accept a MiniBus message instance, message type, headers, and optional scheduling metadata, then return a `ServiceBusMessage`. The factory will serialize the body using `IMessageSerializer`, apply `ContentType`, `MessageId`, `CorrelationId`, and supported Service Bus system properties, and copy MiniBus headers into `ApplicationProperties`.

**Rationale:** Send, publish, and schedule must produce equivalent Service Bus envelopes. Centralizing this logic prevents divergent header or serialization behavior across operations.

**Alternatives considered:**
- Build `ServiceBusMessage` inline in each dispatch method: rejected because header and serialization behavior would be easy to drift.
- Store metadata only in application properties: rejected because Service Bus has useful system properties such as message id, correlation id, content type, and subject that should mirror MiniBus headers where possible.

### 5. Treat Service Bus application properties as the canonical transport carrier for MiniBus headers

Outgoing messages will copy MiniBus headers into application properties using the same header keys. Incoming mapping support will convert Service Bus application properties back into a string header dictionary, accepting primitive property values and converting them with invariant culture where needed.

**Rationale:** Application properties are the Service Bus mechanism intended for user metadata and subscription filters. Keeping the existing MiniBus header names makes filters and receive adapters predictable.

**Alternatives considered:**
- Prefix or rename all properties for Service Bus: rejected because the architecture already defines `MiniBus.*` header keys.
- Serialize headers as one JSON application property: rejected because Service Bus subscription filters need individual application properties.

### 6. Keep integration testing out of the initial transport change

Tests will validate dispatch behavior through mocked sender abstractions and inspect `ServiceBusMessage` instances produced by the factory. Live Service Bus tests will be deferred until the repository has explicit integration-test infrastructure and configuration.

**Rationale:** Unit tests can cover the MiniBus behavior introduced here without creating brittle dependency on cloud resources or developer-specific configuration.

**Alternatives considered:**
- Add live namespace tests now: rejected because the scope explicitly excludes real end-to-end tests unless infrastructure already exists.

## Risks / Trade-offs

- **[Risk] Core command routing is currently internal, so the transport may need a narrow public route resolver or duplicate configuration path.** → **Mitigation:** expose only the smallest stable abstraction needed by transport dispatch, or keep route configuration in the transport package if that avoids prematurely freezing core internals.
- **[Risk] Service Bus application property value types are limited.** → **Mitigation:** map MiniBus headers as strings and convert received primitive values to strings deterministically.
- **[Risk] Scheduled messages need a destination even for generic `IMessage` instances.** → **Mitigation:** fail fast when no schedule destination can be resolved rather than guessing a queue or topic.
- **[Risk] Event topic routing may need richer topology later.** → **Mitigation:** start with explicit event-to-topic mappings and a shared-topic-friendly model that can later grow to conventions or topology setup.

## Migration Plan

1. Add the `MiniBus.AzureServiceBus` project and include it in `MiniBus.sln`.
2. Add Azure Service Bus SDK dependency to the new project only.
3. Implement transport routing/configuration, sender abstraction, header mapper, message factory, and dispatch service.
4. Add `MiniBus.AzureServiceBus.Tests` with mocked sender tests and direct message factory/header mapper tests.
5. Build the solution and run core plus Azure Service Bus transport tests.

Rollback is straightforward before consumers depend on the new package: remove the new project, tests, solution entries, and capability artifacts.

## Open Questions

- Should the implementation expose a public core route resolver for commands, or should transport configuration own command route mappings until a DI registration model exists?
- What exact public name should represent generic scheduled destinations: route name, entity path, or a dedicated schedule routing registry?
- Should event routing support a default shared topic option in addition to explicit per-event mappings in this first transport package?
