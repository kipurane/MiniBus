# MiniBus — Project Architecture and Implementation Context

## 1. Project summary

**Project name:** MiniBus  
**Primary language:** C#  
**Primary runtime target:** .NET 10, with `LangVersion=latest` where project files opt in  
**Primary hosting model:** Azure Functions isolated worker  
**Primary transport:** Azure Service Bus  
**Current persistence options:** SQL Server / Azure SQL, Azure Blob Storage<br>
**Development style:** OpenSpec-driven implementation with GitHub Copilot

MiniBus is a lightweight message-processing framework that mimics common patterns from similar message bus frameworks. It is not intended to be a full clone of any existing product. The goal is to provide a small, Azure-native framework for message-driven .NET applications running on Azure Functions with Azure Service Bus.

The framework should hide repetitive messaging infrastructure concerns while keeping the application code simple, testable, and explicit.

MiniBus should provide:

- Message contracts for commands, events, and generic messages.
- Handler discovery and execution.
- Azure Service Bus send and publish support.
- Azure Functions trigger adapter.
- Message headers, correlation, and causation.
- Recoverability with immediate retries, delayed retries, and dead-lettering.
- SQL-backed inbox and outbox for production reliability.
- Optional saga/state-machine support, with core abstractions before production persistence.
- Optional Azure Storage support for large payloads and low-cost metadata.
- Observability through structured logging and OpenTelemetry-friendly activities.
- Roslyn analyzers, source-generated Function wrappers, project templates, and handler testing helpers.

---

## 2. Design intent

MiniBus should mimic the spirit of common message-processing frameworks, especially:

- Endpoint-oriented architecture.
- Handler-based message processing.
- Command/event distinction.
- Explicit routing.
- Durable pub/sub.
- Reliable processing with retry and dead-letter behavior.
- Idempotent processing.
- Outbox-based consistency.
- Saga-style long-running workflows.
- Testable business handlers independent of transport details.

MiniBus should avoid becoming a large enterprise service bus product. It should remain small enough that a developer can understand the full framework internals.

---

## 3. Core architectural principle

Azure Functions should be treated as a **thin transport adapter**.

Business handlers should not directly depend on:

- `ServiceBusReceivedMessage`
- `ServiceBusMessageActions`
- Azure Functions binding attributes
- Function execution context
- Azure SDK transport concerns

Instead, the flow should be:

```text
Azure Function trigger
        ↓
MiniBus Azure Functions adapter
        ↓
MiniBus processor
        ↓
Deserialize message
        ↓
Load headers and context
        ↓
Apply pipeline behaviors
        ↓
Invoke application handlers
        ↓
Persist inbox/outbox/saga state when configured
        ↓
Dispatch outgoing messages directly, or capture them into SQL outbox
        ↓
Complete, schedule retry, dead-letter, or propagate failure
```

---

## 4. High-level architecture

```text
┌─────────────────────────────────────────────────────────────┐
│ Azure Function App                                          │
│                                                             │
│  ┌───────────────────────────────────────────────────────┐  │
│  │ Service Bus Trigger Function                          │  │
│  │ - Queue trigger                                       │  │
│  │ - Topic subscription trigger                          │  │
│  └───────────────────────────┬───────────────────────────┘  │
│                              │                              │
│                              ▼                              │
│  ┌───────────────────────────────────────────────────────┐  │
│  │ MiniBus.AzureFunctions                                │  │
│  │ - Converts ServiceBusReceivedMessage to MiniBus input │  │
│  │ - Handles Function-specific settlement                │  │
│  └───────────────────────────┬───────────────────────────┘  │
│                              │                              │
│                              ▼                              │
│  ┌───────────────────────────────────────────────────────┐  │
│  │ MiniBus.Core                                          │  │
│  │ - Message context                                     │  │
│  │ - Handler invocation                                  │  │
│  │ - Pipeline behaviors                                  │  │
│  │ - Routing                                             │  │
│  │ - Serialization                                       │  │
│  │ - Recoverability model                                │  │
│  └───────────────────────────┬───────────────────────────┘  │
│                              │                              │
│        ┌─────────────────────┼─────────────────────┐        │
│        ▼                     ▼                     ▼        │
│ ┌───────────────┐     ┌───────────────┐     ┌─────────────┐ │
│ │ Azure Service │     │ SQL           │     │ Azure       │ │
│ │ Bus adapter   │     │ persistence   │     │ Storage     │ │
│ │               │     │               │     │ persistence │ │
│ │ Send          │     │ Inbox         │     │ Blob payload│ │
│ │ Publish       │     │ Outbox        │     │ Claim check │ │
│ │ Schedule      │     │ Saga state    │     │ Audit blobs │ │
│ └───────────────┘     └───────────────┘     └─────────────┘ │
└─────────────────────────────────────────────────────────────┘
```

When SQL outbox is enabled, dispatch is a separate application-owned activity over `SqlMiniBusOutboxDispatcher`. Supported scheduling models include manual commands, optional hosted-service dispatch for worker-style hosts, dedicated worker processes, and timer-triggered Azure Functions dispatchers.

---

## 5. Project/package layout

Current solution structure. The implementation includes core messaging, Azure Service Bus transport, Azure Functions processing, source-generated Function wrappers, analyzers, SQL persistence, Azure Storage persistence, testing helpers, templates, and local reference samples. Some older conceptual package names, such as a standalone observability package, were folded into the runtime packages instead of becoming separate projects.

```text
MiniBus.sln

/src
  /MiniBus.Core
    Auditing
    ClaimCheck
    Contracts
    Context
    Handlers
    Headers
    Persistence
    Recoverability
    Routing
    Sagas
    Serialization

  /MiniBus.AzureServiceBus
    Dispatching
    Recoverability
    Routing
    TransportMessageMapping

  /MiniBus.AzureFunctions
    DependencyInjection
    Processing
    Settlement

  /MiniBus.AzureFunctions.SourceGenerators

  /MiniBus.Analyzers

  /MiniBus.Persistence.Sql
    DependencyInjection
    Schema

  /MiniBus.Persistence.AzureStorage
    DependencyInjection

  /MiniBus.Tooling.Core

  /MiniBus.Tooling.Sql

  /MiniBus.Tooling.Cli

  /MiniBus.Tooling.Web

  /MiniBus.Testing

  /MiniBus.Templates

/samples
  /MiniBus.Samples.Contracts
  /MiniBus.Samples.Billing.FunctionApp
  /MiniBus.Samples.Inventory.FunctionApp

/tests
  /MiniBus.AcceptanceTests
  /MiniBus.Analyzers.Tests
  /MiniBus.Core.Tests
  /MiniBus.AzureServiceBus.Tests
  /MiniBus.AzureFunctions.Tests
  /MiniBus.AzureFunctions.SourceGenerators.Tests
  /MiniBus.Persistence.AzureStorage.Tests
  /MiniBus.Persistence.Sql.Tests
  /MiniBus.Tooling.Core.Tests
  /MiniBus.Tooling.Sql.Tests
  /MiniBus.Tooling.Cli.Tests
  /MiniBus.Templates.Tests
  /MiniBus.Testing.Tests

/openspec
  project.md
  /changes
```

---

## 6. Main concepts

### 6.1 Endpoint

An endpoint is a logical message processor.

In MiniBus, an endpoint usually maps to:

```text
One Azure Function App
One logical input queue
One set of handlers
One endpoint name
One persistence configuration
```

SQL outbox dispatch can live outside the endpoint Function App. For production-style clarity, a separate timer-triggered dispatcher Function App can own draining the endpoint's SQL outbox while the processing Function App owns Service Bus trigger handling.

Example:

```text
Endpoint: Billing
Input queue: billing-queue
Handles:
  - CreateInvoice
  - CancelInvoice
  - PaymentReceived
Publishes:
  - InvoiceCreated
  - InvoiceCancelled
```

### 6.2 Message

All MiniBus messages implement `IMessage`.

```csharp
public interface IMessage
{
}
```

### 6.3 Command

A command asks one logical endpoint to perform work.

```csharp
public interface ICommand : IMessage
{
}
```

Rules:

- A command should have exactly one logical owner.
- A command is sent to a queue.
- A command should usually be named imperatively.

Examples:

```csharp
public sealed record CreateInvoice(Guid InvoiceId, Guid CustomerId, decimal Amount) : ICommand;
public sealed record ReserveInventory(Guid OrderId, IReadOnlyCollection<OrderLine> Lines) : ICommand;
```

### 6.4 Event

An event announces that something already happened.

```csharp
public interface IEvent : IMessage
{
}
```

Rules:

- An event may have zero, one, or many subscribers.
- An event should be published to a topic.
- An event should usually be named in past tense.

Examples:

```csharp
public sealed record InvoiceCreated(Guid InvoiceId, Guid CustomerId) : IEvent;
public sealed record InventoryReserved(Guid OrderId) : IEvent;
```

### 6.5 Handler

A handler processes one message type.

```csharp
public interface IHandleMessages<in TMessage>
    where TMessage : IMessage
{
    Task Handle(TMessage message, MiniBusContext context, CancellationToken cancellationToken);
}
```

Handlers should:

- Contain application-specific business logic.
- Use injected dependencies through constructor injection.
- Use `MiniBusContext` for outgoing messages.
- Avoid direct Azure Service Bus SDK dependencies.
- Be easy to unit test.

---

## 7. MiniBusContext

`MiniBusContext` is the main API available inside handlers.

Initial API:

```csharp
public abstract class MiniBusContext
{
    public abstract string EndpointName { get; }

    public abstract string MessageId { get; }

    public abstract string CorrelationId { get; }

    public abstract string? CausationId { get; }

    public abstract IReadOnlyDictionary<string, string> Headers { get; }

    public abstract Task Send<TCommand>(
        TCommand command,
        CancellationToken cancellationToken = default)
        where TCommand : ICommand;

    public abstract Task Publish<TEvent>(
        TEvent @event,
        CancellationToken cancellationToken = default)
        where TEvent : IEvent;

    public abstract Task Schedule<TMessage>(
        TMessage message,
        DateTimeOffset dueTime,
        CancellationToken cancellationToken = default)
        where TMessage : IMessage;
}
```

Important design decision:

- `Send`, `Publish`, and `Schedule` do not necessarily send immediately.
- When outbox is enabled, outgoing operations are captured and persisted first.
- Dispatching happens only after successful business processing and SQL commit.
- When SQL outbox is not enabled, the Azure Functions adapter dispatches outgoing operations directly through the Azure Service Bus transport.
- When SQL outbox is enabled, applications choose the drain scheduler: manual command, hosted service, dedicated worker process, or timer-triggered Function.

---

## 8. Message headers

MiniBus should store metadata in Azure Service Bus application properties.

Recommended headers:

```text
MiniBus.MessageId
MiniBus.MessageType
MiniBus.EnclosedMessageTypes
MiniBus.EndpointName
MiniBus.CorrelationId
MiniBus.CausationId
MiniBus.ConversationId
MiniBus.ReplyTo
MiniBus.OriginatingEndpoint
MiniBus.OriginatingMachine
MiniBus.ContentType
MiniBus.SchemaVersion
MiniBus.Retry.ImmediateAttempt
MiniBus.Retry.DelayedAttempt
MiniBus.Retry.MaxImmediateAttempts
MiniBus.Retry.MaxDelayedAttempts
MiniBus.OriginalMessageId
MiniBus.Exception.Type
MiniBus.Exception.Message
MiniBus.Exception.StackTraceHash
MiniBus.CreatedUtc
```

Header responsibilities:

- `MessageId`: stable identity for idempotency.
- `CorrelationId`: groups related messages.
- `CausationId`: points to the message that caused the current outgoing message.
- `ConversationId`: groups an end-to-end business conversation.
- `MessageType`: resolves the concrete .NET contract type.
- `SchemaVersion`: supports contract evolution.

---

## 9. Serialization

Default serializer:

```text
System.Text.Json
```

Initial conventions:

- Use UTF-8 JSON.
- Use explicit message type headers.
- Prefer records for immutable message contracts.
- Avoid framework-specific types in message contracts.
- Keep message contracts small.
- Use Blob Storage claim-check for large payloads.

Future extension point:

```csharp
public interface IMessageSerializer
{
    BinaryData Serialize(object message, Type messageType);

    object Deserialize(BinaryData body, Type messageType);
}
```

---

## 10. Routing

Routing maps message types to Azure Service Bus entities.

### 10.1 Command routing

Commands are routed to queues.

Example configuration:

```csharp
services.AddMiniBus(options =>
{
    options.EndpointName = "Billing";

    options.Routing.MapCommand<CreateInvoice>("billing-queue");
    options.Routing.MapCommand<ReserveInventory>("inventory-queue");
});
```

Rules:

- Every command must have one route.
- Missing command route should fail fast at startup or before send.
- Duplicate conflicting command routes should fail at startup.

### 10.2 Event routing

Events are published to topics.

Example:

```csharp
services.AddMiniBus(options =>
{
    options.Routing.PublishToTopic<InvoiceCreated>("domain-events");
});
```

Initial recommendation:

```text
Use one shared topic called domain-events.
Use application properties and subscription filters for event type filtering.
```

Alternative future option:

```text
Use one topic per event type.
```

---

## 11. Azure Functions integration

MiniBus should provide a processor that can be called from an Azure Function trigger.

Example manual adapter:

```csharp
public sealed class BillingFunction
{
    private readonly MiniBusProcessor _processor;

    public BillingFunction(MiniBusProcessor processor)
    {
        _processor = processor;
    }

    [Function("BillingInput")]
    public Task Run(
        [ServiceBusTrigger("billing-queue", Connection = "ServiceBus")]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions actions,
        CancellationToken cancellationToken)
    {
        return _processor.ProcessAsync(
            message,
            actions,
            cancellationToken);
    }
}
```

Important constraint:

Azure Functions trigger bindings are static. MiniBus cannot fully hide trigger declarations at runtime.

Preferred developer experience for later phase:

- Source generator creates trigger wrapper functions from MiniBus endpoint configuration.
- Manual adapter remains supported for clarity and debugging.

---

## 12. Processing pipeline

MiniBus should use a pipeline model before production inbox, outbox, observability, and richer recoverability behavior are layered in.

Current MVP processing is still partly orchestrated directly by `MiniBusProcessor`: receive metadata is adapted, the message is deserialized, regular handlers run, optional saga handlers run, recoverability decides failures, and settlement is applied. The pipeline below is the target refactor that should make those responsibilities independently testable.

```csharp
public delegate Task MiniBusPipelineDelegate();

public interface IMiniBusBehavior
{
    Task Invoke(MiniBusPipelineContext context, MiniBusPipelineDelegate next);
}
```

Initial pipeline:

```text
TransportReceiveBehavior
DeserializeBehavior
HeaderBehavior
CorrelationBehavior
LoggingBehavior
ImmediateRetryBehavior
InboxBehavior
OutboxBehavior
SagaBehavior
HandlerInvocationBehavior
OutgoingDispatchBehavior
SettlementBehavior
```

Design rule:

Each behavior should have one responsibility and should be testable in isolation.

---

## 13. Recoverability

MiniBus should support predictable recoverability.

### 13.1 Immediate retries

Immediate retries happen in memory within the same function invocation.

Example:

```text
ImmediateRetries = 3
```

Flow:

```text
Handler throws
  ↓
Retry same handler execution immediately
  ↓
If still failing, continue to delayed retry
```

### 13.2 Delayed retries

Delayed retries should use Azure Service Bus scheduled messages.

Example:

```text
DelayedRetries:
  10 seconds
  1 minute
  5 minutes
```

Flow:

```text
Handler fails after immediate retries
  ↓
Create copy of incoming message
  ↓
Increment MiniBus.Retry.DelayedAttempt
  ↓
Schedule message for later
  ↓
Complete original after the retry copy is accepted
```

If delayed retry scheduling or completion fails, the processor should propagate the failure to the host. Stronger atomicity between retry scheduling, business state, and settlement requires the SQL inbox/outbox phase.

### 13.3 Dead-lettering

After retry policy is exhausted, MiniBus should dead-letter the message with useful metadata.

Dead-letter reason examples:

```text
MiniBus.RetriesExhausted
MiniBus.UnhandledMessageType
MiniBus.DeserializationFailed
MiniBus.HandlerFailed
MiniBus.SagaConcurrencyExceeded
```

Dead-letter description should include:

```text
Exception type
Exception message
Handler type
Message type
CorrelationId
MiniBus retry attempt
```

---

## 14. Inbox

The inbox prevents duplicate processing.

Purpose:

```text
If Azure Functions retries the same Service Bus message,
or the message is delivered more than once,
MiniBus should not repeat business side effects.
```

SQL table concept:

```sql
CREATE TABLE MiniBusInbox
(
    EndpointName nvarchar(200) NOT NULL,
    MessageId nvarchar(200) NOT NULL,
    ProcessedUtc datetime2 NOT NULL,
    HeadersJson nvarchar(max) NULL,

    CONSTRAINT PK_MiniBusInbox PRIMARY KEY (EndpointName, MessageId)
);
```

Processing behavior:

```text
1. Read endpoint name and message ID.
2. Check inbox.
3. If already processed, complete incoming message.
4. If not processed, allow handler execution.
5. Insert inbox record in same transaction as business data and outbox operations.
```

---

## 15. Outbox

The outbox is mandatory for serious workflows.

Purpose:

```text
Ensure database changes and outgoing messages are committed consistently.
```

Problem it solves:

```text
Business data saved, outgoing message not sent.
Outgoing message sent, business data not saved.
Duplicate outgoing message after crash.
```

SQL table concept:

```sql
CREATE TABLE MiniBusOutbox
(
    OutboxId uniqueidentifier NOT NULL,
    EndpointName nvarchar(200) NOT NULL,
    IncomingMessageId nvarchar(200) NOT NULL,
    OperationsJson nvarchar(max) NOT NULL,
    CreatedUtc datetime2 NOT NULL,
    DispatchedUtc datetime2 NULL,
    ExpiresUtc datetime2 NULL,

    CONSTRAINT PK_MiniBusOutbox PRIMARY KEY (OutboxId)
);

CREATE INDEX IX_MiniBusOutbox_Undispatched
ON MiniBusOutbox (EndpointName, DispatchedUtc, CreatedUtc);
```

Outbox processing:

```text
1. Handler calls context.Send or context.Publish.
2. MiniBus records outgoing operation in memory.
3. MiniBus persists outgoing operations to MiniBusOutbox inside SQL transaction.
4. SQL transaction commits.
5. A dispatcher claims committed outbox rows later.
6. The dispatcher sends outgoing messages through the configured transport.
7. MiniBus marks outbox rows as dispatched.
```

Dispatcher scheduling choices:

```text
Manual command:
  Application resolves SqlMiniBusOutboxDispatcher and drains on demand.

Hosted service:
  AddMiniBusSqlHostedOutboxDispatch registers an opt-in BackgroundService.

Timer-triggered Function:
  Active backlog item. Preferred first Azure Functions-native automatic drain shape,
  especially as a separate dispatcher Function App for production-style clarity.

Dedicated worker:
  Application hosts the same dispatcher in an independently scaled process.
```

Crash behavior:

```text
Crash before SQL commit:
  No side effects committed. Incoming message retries.

Crash after SQL commit but before dispatch:
  Inbox/outbox records exist. Outbox dispatcher can send later.

Crash after dispatch but before mark dispatched:
  Outgoing message may be sent again. Use deterministic MessageId and receiver inbox.

Crash after mark dispatched but before incoming complete:
  Incoming message may retry. Inbox prevents duplicate business processing.
```

---

## 16. Saga support

Sagas are long-running workflows with persisted state.

Current MVP status:

- Core saga contracts, explicit correlation, saga invocation, and persistence abstractions exist.
- `InMemorySagaPersistence` is intended for tests and samples only.
- Production SQL saga persistence exists in `MiniBus.Persistence.Sql`, with optimistic concurrency and explicit schema scripts.

Initial saga abstraction:

```csharp
public abstract class MiniBusSaga<TData>
    where TData : class, new()
{
    public TData Data { get; protected set; } = new();

    public abstract void ConfigureHowToFindSaga(SagaMapper<TData> mapper);
}
```

Saga data requirements:

```csharp
public interface ISagaData
{
    Guid Id { get; set; }

    string CorrelationId { get; set; }
}
```

SQL saga table concept:

```sql
CREATE TABLE MiniBusSaga
(
    SagaType nvarchar(500) NOT NULL,
    CorrelationId nvarchar(500) NOT NULL,
    DataJson nvarchar(max) NOT NULL,
    CreatedUtc datetime2 NOT NULL,
    UpdatedUtc datetime2 NOT NULL,
    Version rowversion NOT NULL,

    CONSTRAINT PK_MiniBusSaga PRIMARY KEY (SagaType, CorrelationId)
);
```

Concurrency model:

```text
Use optimistic concurrency.
If concurrent updates happen, one wins and the other fails.
Failed concurrent update should go through recoverability.
```

Optional transport-level ordering:

```text
For saga-heavy workflows, Service Bus sessions may be used.
Set SessionId = saga correlation ID.
```

---

## 17. Scheduled messages and timeouts

MiniBus should support delayed delivery through Azure Service Bus scheduled messages.

API:

```csharp
await context.Schedule(
    new CancelOrderIfPaymentMissing(orderId),
    DateTimeOffset.UtcNow.AddMinutes(15),
    cancellationToken);
```

Use cases:

- Saga timeouts.
- Delayed retries.
- Follow-up work.
- Reminder messages.

Initial implementation:

```text
Use ServiceBusSender.ScheduleMessageAsync.
```

Future implementation:

```text
Optional SQL timeout table with dispatcher.
```

---

## 18. Large payloads / Claim Check

Service Bus messages should stay small.

MiniBus should support optional claim-check behavior:

```text
If serialized body exceeds configured threshold:
  1. Store payload in Azure Blob Storage.
  2. Replace message body with compact claim-check envelope metadata.
  3. Add MiniBus.ClaimCheck.* headers.
  4. Receiver downloads payload before deserialization.
```

Configuration example:

```csharp
services.AddMiniBusAzureStoragePersistence(
    connectionString,
    containerName: "minibus-payloads",
    options =>
    {
        options.BlobNamePrefix = "payloads";
        options.PayloadRetention = TimeSpan.FromDays(7);
    })
    .AddMiniBusAzureBlobClaimCheck(payloadThresholdBytes: 128 * 1024);
```

---

## 19. Observability

MiniBus should provide first-class diagnostics.

Minimum observability:

- Structured logs.
- Correlation ID in every log entry.
- Message type.
- Endpoint name.
- Handler type.
- Retry attempt.
- Processing duration.
- Outcome: completed, retried, dead-lettered, skipped as duplicate.
- Outbox dispatch duration.
- Saga correlation ID when available.

Recommended trace model:

```text
Activity: MiniBus.Process
  Tags:
    messaging.system = azure_service_bus
    messaging.destination.name = queue/topic
    minibus.endpoint = Billing
    minibus.message_type = CreateInvoice
    minibus.message_id = ...
    minibus.correlation_id = ...
```

---

## 20. Configuration model

Current MVP registration shape:

```csharp
services.AddMiniBusAzureFunctions(options =>
{
    options.EndpointName = "Billing";
    options.EnableSagas = true;
    options.Recoverability.ImmediateRetries = 3;
    options.Recoverability.DelayedRetries.Add(TimeSpan.FromSeconds(10));
    options.Recoverability.DelayedRetries.Add(TimeSpan.FromMinutes(1));
    options.Recoverability.DelayedRetries.Add(TimeSpan.FromMinutes(5));
    options.Recoverability.DeadLetterAfterRetriesExhausted = true;
});
```

Future unified configuration shape, after SQL persistence and transport registration are centralized:

```csharp
services.AddMiniBus(options =>
{
    options.EndpointName = "Billing";

    options.UseSystemTextJson();

    options.UseAzureServiceBus(serviceBus =>
    {
        serviceBus.ConnectionName = "ServiceBus";
        serviceBus.InputQueue = "billing-queue";
        serviceBus.EventsTopic = "domain-events";
    });

    options.UseSqlPersistence(sql =>
    {
        sql.ConnectionStringName = "MiniBusSql";
        sql.EnableInbox = true;
        sql.EnableOutbox = true;
        sql.EnableSagas = true;
    });

    options.Routing.MapCommand<CreateInvoice>("billing-queue");
    options.Routing.MapCommand<ReserveInventory>("inventory-queue");

    options.Routing.PublishToTopic<InvoiceCreated>("domain-events");

    options.Recoverability.ImmediateRetries = 3;
    options.Recoverability.DelayedRetries =
    [
        TimeSpan.FromSeconds(10),
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5)
    ];
    options.Recoverability.DeadLetterAfterRetriesExhausted = true;
});
```

---

## 21. Coding conventions

### 21.1 General

- Use C# 10.
- Prefer nullable reference types.
- Prefer small focused classes.
- Use constructor injection.
- Avoid service locator usage.
- Avoid static mutable state.
- Prefer explicit configuration over hidden conventions.
- Use internal types where public API is not required.
- Public API should be stable and minimal.

### 21.2 Async

- All I/O must be async.
- Public async methods must accept `CancellationToken` unless there is a strong reason not to.
- Avoid sync-over-async.
- Avoid `Task.Result` and `.Wait()`.

### 21.3 Exceptions

- Framework exceptions should be specific.
- Do not swallow exceptions silently.
- Recoverability behavior decides whether to retry, schedule, or dead-letter.
- Handler exceptions should preserve original exception information.

### 21.4 Serialization

- Message contracts should avoid behavior.
- Message contracts should not depend on EF Core entities.
- Message contracts should not expose Azure SDK types.
- Use records for immutable messages where practical.

### 21.5 Testing

- Core pipeline behavior should be unit tested.
- Azure Service Bus integration should have integration tests.
- SQL persistence should have integration tests.
- Handlers should be testable with `MiniBus.Testing`.

---

## 22. Testing strategy

### 22.1 Unit tests

Test:

- Routing lookup.
- Header propagation.
- Serialization and deserialization.
- Handler discovery.
- Pipeline ordering.
- Retry policy decisions.
- Inbox duplicate detection logic.
- Outbox operation capture.
- Saga mapping.

### 22.2 Integration tests

Test with:

- Azure Service Bus emulator or real test namespace.
- SQL Server container or LocalDB.
- Azurite for Blob/Table storage where possible.

Integration test scenarios:

```text
Send command to queue and invoke handler.
Publish event to topic and receive through subscription.
Handler throws and immediate retry is attempted.
Handler throws and delayed retry is scheduled.
Retries exhausted and message is dead-lettered.
Outbox persists outgoing message before dispatch.
Duplicate incoming message is skipped.
Saga state is created and updated.
Saga concurrency conflict is retried.
Large payload is stored in Blob Storage.
```

### 22.3 Handler testing API

Future `MiniBus.Testing` helper example:

```csharp
var context = new TestableMiniBusContext();

var handler = new CreateInvoiceHandler(dbContext);

await handler.Handle(
    new CreateInvoice(invoiceId, customerId, 100m),
    context,
    CancellationToken.None);

context.PublishedMessages
    .ShouldContainSingle<InvoiceCreated>();
```

---

## 23. MVP implementation plan

### Phase 1 — Core abstractions

Implement:

- `IMessage`
- `ICommand`
- `IEvent`
- `IHandleMessages<TMessage>`
- `MiniBusContext`
- `IMessageSerializer`
- `SystemTextJsonMessageSerializer`
- Foundational options needed by processors and transports
- Routing registry
- Handler registry
- Basic handler invocation

Out of scope:

- Outbox
- Sagas
- Delayed retries
- Source generator

### Phase 2 — Azure Service Bus transport

Implement:

- Service Bus message creation.
- Header mapping to application properties.
- Queue send.
- Topic publish.
- Scheduled send.
- Message type resolution.

Out of scope:

- Advanced retry.
- Sessions.
- Batch sending.

### Phase 3 — Azure Functions adapter

Implement:

- `MiniBusProcessor`.
- Adapter from `ServiceBusReceivedMessage`.
- Adapter from `ServiceBusMessageActions`.
- Manual trigger wrapper pattern.
- Basic settlement behavior that later composes with recoverability.

Out of scope:

- Source-generated functions.

### Phase 4 — Recoverability

Implement:

- Immediate retry behavior.
- Delayed retry behavior.
- Dead-letter behavior.
- Retry headers.
- Exception metadata.

### Phase 5 — Saga abstractions

Implement:

- Saga base type.
- Saga data contract.
- Saga correlation mapping.
- Saga persistence abstraction.
- Optimistic-concurrency-ready persistence metadata.
- In-memory persistence for tests and samples.

Out of scope:

- Saga timeout-specific APIs.
- Service Bus sessions.

### Phase 6 — Processing pipeline refactor

Implement:

- Pipeline context.
- Transport receive behavior.
- Deserialization and header behaviors.
- Correlation behavior.
- Recoverability behavior.
- Saga behavior.
- Handler invocation behavior.
- Outgoing dispatch behavior.
- Settlement behavior.

This phase should land before production inbox/outbox and observability so those features can plug into explicit behavior boundaries instead of expanding `MiniBusProcessor`.

### Phase 7 — SQL persistence and production reliability

Implemented baseline:

- SQL connection abstraction.
- First-class SQL Server/Azure SQL provider support with `Microsoft.Data.SqlClient`, connection-string registration, and SQL Server-backed integration tests.
- Transaction boundary shared by MiniBus persistence and business data where configured.
- Inbox table.
- Outbox table.
- Outbox dispatch.
- Deterministic outgoing message IDs for outbox replay.
- SQL saga persistence.
- Explicit SQL schema scripts.
- Cleanup policy.
- Optional hosted-service outbox dispatch.
- Timer-triggered Azure Functions SQL outbox dispatcher reference path.

### Phase 8 — Azure Storage support

Implemented baseline:

- Blob payload store.
- Receive-side claim-check resolution.
- Audit blob writer.

Deferred:

- Optional Table Storage inbox.
- Optional Table Storage saga store.

### Phase 9 — Observability

Implemented inside the current runtime packages:

- Structured logging integration.
- OpenTelemetry-friendly activities.
- Correlation-aware log scopes.
- Metrics for processing, retries, dead-lettering, outbox dispatch, and saga handling.

### Phase 10 — Developer experience

Implemented baseline:

- Source generator for function wrappers.
- Roslyn analyzers.
- Templates.
- Sample apps.
- Documentation.
- Testing helpers.

### Phase 11 — Operational tooling

Implemented baseline:

- Provider-neutral tooling core for read models, filters, timeline fragments, reader interfaces, and explicit action contracts.
- SQL tooling readers for inbox, outbox, and saga state.
- Best-effort SQL message/correlation timeline assembly.
- Bounded SQL outbox drain action wrapper over `SqlMiniBusOutboxDispatcher`.
- First CLI command surface for local SQL troubleshooting with table and JSON output.
- Packaged `MiniBus.Tooling.Web` surface with a read-only ASP.NET Core Minimal API and React/TypeScript UI for local list, detail, and timeline inspection.
- Documentation for local SQL configuration, read-only inspection, explicit actions, redaction defaults, and deferred tooling surfaces.

Planned direction:

- Build CLI and UI as two front doors over the same tooling core, not as separate implementations.
- Use CLI commands for repeatable local troubleshooting, scripted operations, CI diagnostics, and safe administrative actions.
- Use a local UI for correlated operational understanding: message timelines, inbox/outbox state, saga state, dispatch outcomes, logs, traces, metrics, and broker state.
- Keep tooling focused on observing and operating MiniBus runtime state; it must not become a second message-processing runtime.
- Prefer provider modules for SQL persistence, Azure Service Bus, and observability backends.
- Evolve `MiniBus.Tooling.Web` with additional read-only providers before adding mutating browser actions.
- Prefer Aspire for local sample orchestration when running SQL Server, Service Bus emulator, Function Apps, dispatcher hosts, and `MiniBus.Tooling.Web` together.
- Keep Aspire as a development/sample orchestration concern rather than a runtime dependency of MiniBus packages.

Conceptual shape:

```text
                  ┌────────────────────────┐
                  │  MiniBus.Tooling.Core  │
                  │  readers + actions     │
                  └───────────┬────────────┘
                              │
          ┌───────────────────┼───────────────────┐
          │                   │                   │
          ▼                   ▼                   ▼
 ┌────────────────┐   ┌────────────────┐   ┌────────────────┐
 │ CLI console    │   │ Tooling Web    │   │ provider       │
 │ scripts/CI/dev │   │ Minimal API    │   │ modules        │
 └────────────────┘   │ + React TS UI  │   └────────────────┘
                      └────────────────┘
```

Initial operational surfaces:

- SQL inbox records: endpoint, logical message id, timestamps, correlation metadata, and duplicate-processing evidence.
- SQL outbox records: pending/claimed/dispatched/failed state, operation kind, due time, attempt metadata, last error, and deterministic outgoing message id.
- SQL saga records: saga type, correlation id, completion state, updated timestamp, version metadata, and serialized state inspection with appropriate redaction boundaries.
- Azure Service Bus entities: configured queues, topics, subscriptions, active/dead-letter counts, and dead-letter peek where credentials allow it.
- Observability: structured MiniBus logs, traces, metrics, and audit records when the application has configured a readable sink.
- Safe actions: bounded outbox drain exists for SQL; failed outbox retry, broker/DLQ peek, and later carefully scoped DLQ resubmission remain future work if requirements justify them.

The main value of the UI is correlation rather than replacing Azure Portal or Service Bus Explorer:

```text
Message 123
  ├─ inbox: processed by Billing endpoint
  ├─ saga: BillingSaga/order-42 updated
  ├─ outbox: InvoiceCreated published
  ├─ broker: message visible in inventory subscription
  └─ logs/traces: handler completed with correlated diagnostics
```

Custom application logs should not be scraped from arbitrary console output. Tooling should read structured logs only through an explicit sink such as local JSON files, OpenTelemetry collector output, Application Insights/Azure Monitor, or a future MiniBus-native audit/event store.

---

## 24. Remaining feature backlog

This list is the updateable checklist of near-term work still needed for MiniBus to become a fully operational framework. Keep it current as OpenSpec changes are proposed, implemented, and archived. Conditional or long-range ideas belong in the deferred feature backlog.

Saga timeout support now uses Service Bus scheduled messages, with SQL outbox capture for durable workflows. The next major framework feature should come from the active backlog below.

### 24.1 Core and processing architecture

- [x] Decide whether to introduce a broader `MiniBusOptions` core configuration object beyond the current package-specific options.
- [x] Refactor `MiniBusProcessor` orchestration into explicit pipeline behaviors.
- [x] Add a pipeline context that can carry received message metadata, deserialized payloads, headers, recoverability state, saga state, outgoing operations, and settlement decisions.
- [x] Add unit tests for pipeline ordering and behavior isolation.

### 24.2 SQL persistence and production reliability

- [x] Create `MiniBus.Persistence.Sql`.
- [x] Add a provider-neutral SQL connection/session abstraction based on caller-provided `DbConnection`.
- [x] Add first-class SQL Server/Azure SQL provider support with `Microsoft.Data.SqlClient`.
- [x] Add connection-string-based registration for SQL Server/Azure SQL while preserving the existing `DbConnection` factory escape hatch.
- [x] Define how MiniBus persistence shares a transaction boundary with business data when configured.
- [x] Implement inbox table schema and duplicate-message detection.
- [x] Complete duplicate messages without re-running business handlers.
- [x] Implement outbox table schema.
- [x] Capture outgoing `Send`, `Publish`, and `Schedule` operations into the outbox.
- [x] Dispatch pending outbox operations after successful processing.
- [x] Add deterministic outgoing message IDs for replay-safe outbox dispatch.
- [x] Mark outbox operations as dispatched and support retry metadata after dispatch failures.
- [x] Harden outbox crash recovery semantics with SQL Server/Azure SQL integration coverage.
- [x] Add cleanup and expiry policy for inbox/outbox records.
- [x] Decide whether migrations are framework-owned or shipped as SQL scripts.
- [x] Add SQL Server/Azure SQL integration tests for schema creation, inbox duplicate detection, outbox capture, outbox replay, transaction behavior, and cleanup.
- [x] Add a high-level SQL outbox dispatch/drain acceptance test that processes the reference workflow, captures SQL outbox rows, runs `SqlMiniBusOutboxDispatcher.DispatchPendingAsync`, and verifies the configured transport receives the expected send, publish, and schedule operations.
- [x] Add opt-in hosted-service SQL outbox dispatch with bounded cycles, startup drain, failure backoff, best-effort wake-up, graceful shutdown behavior, and tests.
- [x] Add timer-triggered Azure Functions SQL outbox dispatcher reference path and sample.

### 24.3 Saga follow-ups

Basic saga contracts, correlation, invocation, in-memory persistence, SQL persistence, and Service Bus-backed timeout behavior exist.

- [x] Implement SQL saga persistence.
- [x] Implement durable optimistic concurrency using SQL version metadata.
- [x] Ensure saga persistence failures and concurrency conflicts flow through recoverability.
- [x] Add saga timeout scheduling APIs or conventions.
- [x] Decide whether saga timeouts use Service Bus scheduled messages only or an optional SQL timeout table.
- [x] Add integration tests for SQL saga load/create/save/complete and concurrency conflicts.
- [x] Add integration tests for saga timeout dispatch.

### 24.4 Azure Storage support

- [x] Create `MiniBus.Persistence.AzureStorage` or equivalent storage package.
- [x] Implement Blob payload store.
- [x] Implement large payload claim-check support.
- [x] Add receive-side claim-check resolution before deserialization.
- [x] Implement audit blob writer.
- [x] Add Testcontainers-backed Azurite or live-resource-gated integration tests for Blob payload storage.

### 24.5 Observability

- [x] Add structured logging integration.
- [x] Add correlation-aware log scopes.
- [x] Add OpenTelemetry-friendly activities/tracing.
- [x] Emit diagnostic metadata such as endpoint name, message type, message id, correlation id, causation id, handler type, retry attempt, delayed retry attempt, saga type, and saga correlation id.
- [x] Add processing outcome diagnostics for completed, retried, delayed, dead-lettered, skipped duplicate, saga-completed, and outbox-dispatched outcomes.
- [x] Add metrics for processing duration, handler duration, retry counts, dead-letter counts, outbox dispatch duration, and saga handling.
- [x] Add tests or verification hooks for log/tracing metadata.

### 24.6 Developer experience

The next sample increment should prefer a local Azure Service Bus emulator path for a runnable reference workflow. Live Azure Service Bus coverage should remain a separate proof layer after the emulator-backed workflow shape is useful and stable.

- [x] Add source generator for Azure Function wrappers.
- [x] Add Roslyn analyzers for common configuration and handler mistakes.
- [x] Add project templates.
- [x] Add package metadata and central build props before real NuGet publishing.
- [x] Add a buildable Azure Functions billing sample project that demonstrates MiniBus registration, handler code, Service Bus routing, recoverability, and saga setup.
- [x] Expand the billing sample into a fuller locally runnable reference app against the Azure Service Bus emulator once the remaining core production features are stable.
- [x] Add an inventory or multi-endpoint sample on top of the emulator-backed reference workflow.
- [x] Add the timer-triggered SQL outbox dispatcher sample.
- [ ] Add live Azure Service Bus integration tests once the emulator-backed sample workflow is stable and reusable Azure infrastructure exists.
- [x] Add documentation for configuration, routing, recoverability, sagas, SQL persistence, outbox behavior, observability, and testing.
- [x] Add a `MiniBus.Testing` package with `TestableMiniBusContext`, fake bus helpers, and handler test harnesses.

### 24.7 Tooling and local operations

The first tooling increment should harden a shared model before investing heavily in UI. CLI and UI should reuse the same core services so command output, API responses, and UI screens describe the same MiniBus state.

- [x] Create a tooling proposal that defines the shared tooling substrate, package/project boundaries, provider model, and first safe operations.
- [x] Add a provider-neutral tooling core for read models and action contracts over inbox, outbox, sagas, broker state, and observability sources.
- [x] Add SQL tooling readers for inbox, outbox, and saga state, including filtering by endpoint, message id, correlation id, status, and time window.
- [x] Add safe SQL outbox operations for bounded drain and retry-oriented troubleshooting over the existing `SqlMiniBusOutboxDispatcher`.
- [ ] Add Azure Service Bus inspection for queues, topics, subscriptions, active/dead-letter counts, and dead-letter peek where credentials allow it.
- [ ] Decide the first structured log source for local tooling, such as JSON log files, OpenTelemetry collector output, or a MiniBus-native audit/event store.
- [x] Add a CLI console app over the shared tooling core for local troubleshooting, scripts, and CI diagnostics.
- [x] Add `MiniBus.Tooling.Web` as a packaged ASP.NET Core Minimal API over the shared tooling core so the UI and future remote tooling use a stable HTTP boundary.
- [x] Add a React and TypeScript UI served by `MiniBus.Tooling.Web`, focused first on read-only correlated message timelines, inbox/outbox state, saga state, and list/detail troubleshooting views.
- [ ] Add Aspire-based local orchestration for the reference samples, SQL Server, Service Bus emulator, dispatcher host, and `MiniBus.Tooling.Web`.
- [x] Document local-only versus Azure-hosted tooling deployment guidance, including credential handling, read/write action safety, and redaction expectations.

---

## 25. Deferred feature backlog

This list captures capabilities that may become valuable later but should not distract from the active framework baseline. Promote an item into the remaining feature backlog only when there is a clear use case, an OpenSpec proposal, and a reason to implement it in the next planning horizon.

- [ ] Add a SQL-managed scheduled-message store and dispatcher if MiniBus needs transport-independent delayed delivery, timeout cancellation/replacement, or operational inspection beyond Azure Service Bus scheduled messages.
- [ ] Add Service Bus sessions support where ordering or saga-heavy workflows need it.
- [ ] Add batch sending if throughput requirements justify it.
- [ ] Add advanced retry and exception classification policies.
- [ ] Add manual retry tooling or dashboard support.
- [ ] Add optional Azure Table Storage inbox and saga persistence if MiniBus needs a SQL-free Azure Storage reliability mode for lightweight/serverless workloads.
- [ ] Decide whether automatic Azure infrastructure provisioning belongs in this framework or in templates/documentation only.
- [ ] Add an optional one-topic-per-event-type topology if the shared topic plus subscription filter model becomes too limiting.
- [ ] Add DLQ resubmission, message replay, or destructive broker actions only after explicit safety, authorization, and audit requirements exist.
- [ ] Add Azure Monitor/Application Insights query integration for hosted UI diagnostics if local structured log reading is not enough.
- [ ] Add a durable MiniBus-native audit/event store if SQL inbox/outbox/saga state plus logs/traces cannot answer message timeline questions reliably.
- [ ] Add remote Azure Container Apps or Container Apps Environment deployment templates for the tooling API/UI if local tooling proves useful enough.

---

## 26. Non-goals

MiniBus should not initially support:

- Multiple transports.
- RabbitMQ.
- Kafka.
- MSMQ.
- Distributed transactions.
- Full compatibility with any existing message bus framework.
- Full saga DSL parity with any existing message bus framework.
- Enterprise-scale graphical service control tooling that attempts to replace Azure Portal, Service Bus Explorer, or a full observability platform.
- Automatic Azure infrastructure provisioning.
- Multi-tenant framework complexity.
- Complex plugin ecosystem.

These can be revisited later only if there is a clear use case.

---

## 27. Important design decisions

### 27.1 Use SQL as the primary reliability store

SQL is the preferred persistence option for:

- Inbox
- Outbox
- Sagas
- Transactional consistency with business data

Azure Storage is useful but should not be the primary consistency mechanism when business data is also in SQL.

### 27.2 Keep Azure Functions as adapter only

The framework should not put business logic in function classes.

Function classes should only call `MiniBusProcessor`.

### 27.3 Prefer explicit routes

Do not rely too much on naming conventions.

Explicit routing makes Copilot-generated code safer and makes production behavior more predictable.

### 27.4 Outbox should be opt-in for MVP, but strongly recommended

During early MVP, simple send/publish can work without outbox.

For production business workflows, outbox should be enabled.

### 27.5 Support manual wrappers before source generation

Source generation improves developer experience, but manual trigger wrappers are easier to debug and should come first.

---

## 28. Example end-to-end flow

Example command:

```csharp
public sealed record CreateInvoice(
    Guid InvoiceId,
    Guid CustomerId,
    decimal Amount) : ICommand;
```

Handler:

```csharp
public sealed class CreateInvoiceHandler : IHandleMessages<CreateInvoice>
{
    private readonly BillingDbContext _dbContext;

    public CreateInvoiceHandler(BillingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task Handle(
        CreateInvoice message,
        MiniBusContext context,
        CancellationToken cancellationToken)
    {
        var invoice = new Invoice
        {
            Id = message.InvoiceId,
            CustomerId = message.CustomerId,
            Amount = message.Amount
        };

        _dbContext.Invoices.Add(invoice);

        await _dbContext.SaveChangesAsync(cancellationToken);

        await context.Publish(
            new InvoiceCreated(message.InvoiceId, message.CustomerId),
            cancellationToken);
    }
}
```

Function wrapper:

```csharp
public sealed class BillingInputFunction
{
    private readonly MiniBusProcessor _processor;

    public BillingInputFunction(MiniBusProcessor processor)
    {
        _processor = processor;
    }

    [Function("BillingInput")]
    public Task Run(
        [ServiceBusTrigger("billing-queue", Connection = "ServiceBus")]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions actions,
        CancellationToken cancellationToken)
    {
        return _processor.ProcessAsync(
            message,
            actions,
            cancellationToken);
    }
}
```

---

## 29. Current planning questions

Resolved early questions:

1. MiniBus is `net10.0`-first for runtime packages, with analyzers/source generators targeting `netstandard2.0`.
2. SQL persistence uses raw ADO.NET and `Microsoft.Data.SqlClient`, not Dapper or EF Core.
3. SQL schema is shipped as explicit scripts rather than framework-owned runtime migrations.
4. Source generation is part of the developer-experience baseline, while manual wrappers remain supported.
5. SQL outbox dispatch is separate from message processing; supported scheduling models include manual drains, hosted-service drains, dedicated workers, and timer-triggered Functions.
6. MiniBus operational tooling should start from a shared tooling core, with CLI and UI as clients over the same read/action model.
7. Aspire is a good fit for local sample orchestration, but it should not become a runtime dependency of MiniBus packages.

Open or deferred questions:

1. Should runtime packages eventually multi-target older supported LTS frameworks?
2. Should Service Bus sessions become first-class when ordering or saga-heavy workflows need them?
3. Should event topology remain a shared topic with subscriptions, or should a one-topic-per-event-type topology be added?
4. Should message schema versioning become a first-class MiniBus concern?
5. Should optional Azure Table Storage inbox/saga persistence become a SQL-free reliability mode?
6. Which structured log source should the first local tooling implementation read?
7. Should MiniBus eventually own a durable audit/event store for message timelines, or rely on configured logging/tracing backends?

---

## 30. Active OpenSpec changes

Current active changes:

```text
add-operational-tooling-foundation
```

Completed but not yet archived changes:

```text
None.
```

---

## 31. Copilot guidance

When using GitHub Copilot, prefer small implementation prompts.

Good prompt pattern:

```text
Implement the next task from openspec/changes/add-core-message-processing/tasks.md.
Follow the architecture in openspec/project.md.
Only modify MiniBus.Core and MiniBus.Core.Tests.
Keep public APIs minimal.
Add unit tests for the new behavior.
```

Avoid broad prompts like:

```text
Build MiniBus.
Implement a clone of an existing message bus framework.
Create the whole framework.
```

Copilot should be used change-by-change, not as a one-shot framework generator.
