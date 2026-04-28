# MiniBus — Project Architecture and Implementation Context

## 1. Project summary

**Project name:** MiniBus  
**Primary language:** C# 10  
**Primary runtime target:** .NET 10 with C# 10 language features  
**Primary hosting model:** Azure Functions isolated worker  
**Primary transport:** Azure Service Bus  
**Persistence options:** SQL Server / Azure SQL, Azure Storage Tables, Azure Blob Storage  
**Development style:** OpenSpec-driven implementation with GitHub Copilot

MiniBus is a lightweight message-processing framework inspired by the most useful ideas from NServiceBus. It is not intended to be a full clone. The goal is to provide a small, Azure-native framework for message-driven .NET applications running on Azure Functions with Azure Service Bus.

The framework should hide repetitive messaging infrastructure concerns while keeping the application code simple, testable, and explicit.

MiniBus should provide:

- Message contracts for commands, events, and generic messages.
- Handler discovery and execution.
- Azure Service Bus send and publish support.
- Azure Functions trigger adapter.
- Message headers, correlation, and causation.
- Recoverability with immediate retries, delayed retries, and dead-lettering.
- SQL-backed inbox and outbox.
- Optional saga/state-machine support.
- Optional Azure Storage support for large payloads and low-cost metadata.
- Observability through structured logging and OpenTelemetry-friendly activities.

---

## 2. Design intent

MiniBus should mimic the spirit of NServiceBus, especially:

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
Persist inbox/outbox/saga state
        ↓
Dispatch outgoing messages
        ↓
Complete, abandon, defer, schedule retry, or dead-letter
```

---

## 4. High-level architecture

```text
┌─────────────────────────────────────────────────────────────┐
│ Azure Function App                                           │
│                                                             │
│  ┌───────────────────────────────────────────────────────┐  │
│  │ Service Bus Trigger Function                           │  │
│  │ - Queue trigger                                        │  │
│  │ - Topic subscription trigger                           │  │
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
│  │ MiniBus.Core                                           │  │
│  │ - Message context                                      │  │
│  │ - Handler invocation                                   │  │
│  │ - Pipeline behaviors                                   │  │
│  │ - Routing                                              │  │
│  │ - Serialization                                        │  │
│  │ - Recoverability model                                 │  │
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

Recommended solution structure:

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
MiniBus.Retry.Attempt
MiniBus.Retry.DelayedAttempt
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
        FunctionContext functionContext,
        CancellationToken cancellationToken)
    {
        return _processor.ProcessAsync(
            message,
            actions,
            functionContext,
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

MiniBus should use a pipeline model.

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
Complete or dead-letter original depending on policy
```

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

Example:

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

Example:

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
- `MiniBusOptions`
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
- Basic complete/dead-letter behavior.

Out of scope:

- Source-generated functions.

### Phase 4 — Recoverability

Implement:

- Immediate retry behavior.
- Delayed retry behavior.
- Dead-letter behavior.
- Retry headers.
- Exception metadata.

### Phase 5 — SQL persistence

Implement:

- SQL connection abstraction.
- Inbox table.
- Outbox table.
- Transaction boundary.
- Outbox dispatch.
- Cleanup policy.

### Phase 6 — Saga support

Implement:

- Saga base type.
- Saga data persistence.
- Saga correlation mapping.
- Optimistic concurrency.
- Saga timeout scheduling.

### Phase 7 — Azure Storage support

Implement:

- Blob payload store.
- Optional Table Storage inbox.
- Optional Table Storage saga store.
- Audit blob writer.

### Phase 8 — Developer experience

Implement:

- Source generator for function wrappers.
- Roslyn analyzers.
- Templates.
- Sample apps.
- Documentation.

---

## 24. Non-goals

MiniBus should not initially support:

- Multiple transports.
- RabbitMQ.
- Kafka.
- MSMQ.
- Distributed transactions.
- Full NServiceBus compatibility.
- Full saga DSL parity with NServiceBus.
- Graphical service control tooling.
- Automatic Azure infrastructure provisioning.
- Multi-tenant framework complexity.
- Complex plugin ecosystem.

These can be revisited later only if there is a clear use case.

---

## 25. Important design decisions

### 25.1 Use SQL as the primary reliability store

SQL is the preferred persistence option for:

- Inbox
- Outbox
- Sagas
- Transactional consistency with business data

Azure Storage is useful but should not be the primary consistency mechanism when business data is also in SQL.

### 25.2 Keep Azure Functions as adapter only

The framework should not put business logic in function classes.

Function classes should only call `MiniBusProcessor`.

### 25.3 Prefer explicit routes

Do not rely too much on naming conventions.

Explicit routing makes Copilot-generated code safer and makes production behavior more predictable.

### 25.4 Outbox should be opt-in for MVP, but strongly recommended

During early MVP, simple send/publish can work without outbox.

For production business workflows, outbox should be enabled.

### 25.5 Support manual wrappers before source generation

Source generation improves developer experience, but manual trigger wrappers are easier to debug and should come first.

---

## 26. Example end-to-end flow

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
        FunctionContext functionContext,
        CancellationToken cancellationToken)
    {
        return _processor.ProcessAsync(
            message,
            actions,
            functionContext,
            cancellationToken);
    }
}
```

---

## 27. Open questions

These should be decided before or during early implementation.

1. Should MiniBus target only Azure Functions isolated worker, or also generic worker services?
2. Should MiniBus target `net6.0` only, or multi-target newer .NET versions?
3. Should SQL persistence use raw ADO.NET, Dapper, or EF Core?
4. Should business data and MiniBus persistence be required to use the same database connection?
5. Should event topology default to one shared topic or one topic per event type?
6. Should sessions be first-class in MVP or deferred?
7. Should source generation be part of MVP or a later developer experience feature?
8. Should the framework own database migrations or expose SQL scripts only?
9. Should message schema versioning be part of the MVP?
10. Should the outbox dispatcher run inside message processing only, or also as a separate timer-triggered function?

---

## 28. First OpenSpec change suggestion

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

## 29. Copilot guidance

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
Implement NServiceBus clone.
Create the whole framework.
```

Copilot should be used change-by-change, not as a one-shot framework generator.
