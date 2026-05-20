# MiniBus — Project Architecture and Implementation Context

## 1. Project summary

**Project name:** MiniBus  
**Primary language:** C# 10  
**Primary runtime target:** .NET 10 with C# 10 language features  
**Primary hosting model:** Azure Functions isolated worker  
**Primary transport:** Azure Service Bus  
**Persistence options:** SQL Server / Azure SQL, Azure Storage Tables, Azure Blob Storage  
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
Dispatch outgoing messages directly or through an outbox
        ↓
Complete, abandon, defer, schedule retry, or dead-letter
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
│ │ Send          │     │ Inbox         │     │ Blob data   │ │
│ │ Publish       │     │ Outbox        │     │ Table state │ │
│ │ Schedule      │     │ Saga state    │     │ Audit blobs │ │
│ └───────────────┘     └───────────────┘     └─────────────┘ │
└─────────────────────────────────────────────────────────────┘
```

---

## 5. Project/package layout

Target solution structure. The current MVP contains the core, Azure Service Bus, Azure Functions, and test projects; persistence, observability, testing helpers, and additional samples are planned phases.

```text
MiniBus.sln

/src
  /MiniBus.Core
    Contracts
    Context
    Dispatching
    Handlers
    Headers
    Pipeline
    Routing
    Serialization
    Recoverability

  /MiniBus.AzureServiceBus
    Sending
    Publishing
    Scheduling
    TransportMessageMapping

  /MiniBus.AzureFunctions
    Processing
    FunctionAdapters
    Settlement

  /MiniBus.Persistence.Sql
    Inbox
    Outbox
    Sagas
    Migrations
    TransactionManagement

  /MiniBus.Persistence.AzureStorage
    Tables
    Blobs
    DataBus
    Audit

  /MiniBus.Observability
    Logging
    Tracing
    Metrics

  /MiniBus.Testing
    TestableMiniBusContext
    FakeBus
    HandlerTestHarness

/samples
  /MiniBus.Samples.Billing
  /MiniBus.Samples.Inventory
  /MiniBus.Samples.FunctionApp

/tests
  /MiniBus.Core.Tests
  /MiniBus.AzureServiceBus.Tests
  /MiniBus.AzureFunctions.Tests
  /MiniBus.Persistence.Sql.Tests
  /MiniBus.IntegrationTests

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
- Dispatching happens after successful business processing.
- Until the outbox package exists, the Azure Functions adapter dispatches outgoing operations directly through the Azure Service Bus transport.

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
5. MiniBus dispatches outgoing messages.
6. MiniBus marks outbox row as dispatched.
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

## 18. Large payloads / DataBus / Claim Check

Service Bus messages should stay small.

MiniBus should support optional claim-check behavior:

```text
If serialized body exceeds configured threshold:
  1. Store payload in Azure Blob Storage.
  2. Replace message body with pointer metadata.
  3. Add MiniBus.DataBus.BlobUri or BlobName header.
  4. Receiver downloads payload before deserialization.
```

Configuration example:

```csharp
options.DataBus.UseAzureBlobStorage(blob =>
{
    blob.ContainerName = "minibus-payloads";
    blob.PayloadThresholdBytes = 128 * 1024;
});
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

Implement:

- SQL connection abstraction.
- First-class SQL Server/Azure SQL provider support with `Microsoft.Data.SqlClient`, connection-string registration, and SQL Server-backed integration tests.
- Transaction boundary shared by MiniBus persistence and business data where configured.
- Inbox table.
- Outbox table.
- Outbox dispatch.
- Deterministic outgoing message IDs for outbox replay.
- SQL saga persistence.
- Migration scripts or owned migration strategy.
- Cleanup policy.

### Phase 8 — Azure Storage support

Implement:

- Blob payload store.
- Optional Table Storage inbox.
- Optional Table Storage saga store.
- Audit blob writer.

### Phase 9 — Observability

Implement:

- Structured logging integration.
- OpenTelemetry-friendly activities.
- Correlation-aware log scopes.
- Metrics for processing, retries, dead-lettering, outbox dispatch, and saga handling.

### Phase 10 — Developer experience

Implement:

- Source generator for function wrappers.
- Roslyn analyzers.
- Templates.
- Sample apps.
- Documentation.

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
- [x] Implement large payload/DataBus/claim-check support.
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

- [x] Add source generator for Azure Function wrappers.
- [x] Add Roslyn analyzers for common configuration and handler mistakes.
- [ ] Add project templates.
- [x] Add package metadata and central build props before real NuGet publishing.
- [x] Add a buildable Azure Functions billing sample project that demonstrates MiniBus registration, handler code, Service Bus routing, recoverability, and saga setup.
- [ ] Expand the billing sample into a fuller runnable reference app once the remaining core production features are stable.
- [ ] Add an inventory or multi-endpoint sample.
- [ ] Add live Azure Service Bus integration tests once reusable infrastructure exists.
- [x] Add documentation for configuration, routing, recoverability, sagas, SQL persistence, outbox behavior, observability, and testing.
- [x] Add a `MiniBus.Testing` package with `TestableMiniBusContext`, fake bus helpers, and handler test harnesses.

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
- Graphical service control tooling.
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

## 29. Open questions

These should be decided before or during early implementation.

1. Should MiniBus target only Azure Functions isolated worker, or also generic worker services?
2. Should MiniBus stay `net10.0`-first or multi-target older supported LTS frameworks?
3. Should SQL persistence use raw ADO.NET, Dapper, or EF Core?
4. Should business data and MiniBus persistence be required to use the same database connection?
5. Should event topology default to one shared topic or one topic per event type?
6. Should sessions be first-class in MVP or deferred?
7. Should source generation be part of MVP or a later developer experience feature?
8. Should the framework own database migrations or expose SQL scripts only?
9. Should message schema versioning be part of the MVP?
10. Should the outbox dispatcher run inside message processing only, or also as a separate timer-triggered function?

---

## 30. First OpenSpec change suggestion

Suggested first OpenSpec change:

```text
openspec/changes/add-core-message-processing/
  proposal.md
  design.md
  tasks.md
  specs/
    minibus-core/spec.md
```

Initial capability:

```text
Capability: MiniBus core message processing

The framework shall provide message contracts, handler discovery,
message serialization, routing, and handler invocation independent
of Azure Functions and Azure Service Bus transport concerns.
```

Suggested first implementation tasks:

```text
1. Create MiniBus.Core project.
2. Add IMessage, ICommand, IEvent.
3. Add IHandleMessages<TMessage>.
4. Add MiniBusContext abstraction.
5. Add routing registry.
6. Add System.Text.Json serializer.
7. Add handler registry and handler invoker.
8. Add unit tests for handler discovery and invocation.
9. Add basic sample handler.
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
