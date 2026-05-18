# MiniBus.Testing

`MiniBus.Testing` contains lightweight helpers for unit testing MiniBus handlers and saga handlers without Azure Functions, Azure Service Bus, SQL persistence, or a real MiniBus processor host.

The first helper is `TestableMiniBusContext`, a concrete `MiniBusContext` that exposes deterministic inbound metadata and captures outgoing `Send`, `Publish`, and `Schedule` calls.

Use this package for direct application handler tests:

- Handler business logic.
- Saga handler behavior once saga data is arranged in memory.
- Outgoing send, publish, and schedule assertions.
- Header, endpoint, message id, correlation id, and causation id expectations.

Use the runtime package test suites or application integration tests when you need to verify processor behavior, Azure Functions settlement, Azure Service Bus dispatch, SQL persistence, Azure Storage payloads, OpenTelemetry listeners, or live infrastructure.

## Handler example

```csharp
var context = new TestableMiniBusContext(
    endpointName: "Billing",
    messageId: "message-1",
    correlationId: "correlation-1");

var handler = new CreateInvoiceHandler();

await handler.Handle(
    new CreateInvoice("invoice-1", "customer-1", 123.45m),
    context,
    CancellationToken.None);

var invoiceCreated = context.SinglePublished<InvoiceCreated>();

Assert.Equal("invoice-1", invoiceCreated.Message.InvoiceId);
```

## Saga handler example

```csharp
var context = new TestableMiniBusContext();
var saga = new BillingSaga();

await saga.Handle(
    new InvoiceCreated("invoice-1", "customer-1", 123.45m),
    context,
    CancellationToken.None);

var timeout = context.SingleScheduled<InvoicePaymentTimeout>();

Assert.True(timeout.DueTime > DateTimeOffset.UtcNow);
```

## Captured operations

Use the raw read-only collections when you want to inspect all captured work:

```csharp
Assert.Single(context.SentMessages);
Assert.Single(context.PublishedMessages);
Assert.Single(context.ScheduledMessages);
```

Use typed query helpers when you care about a specific message type:

```csharp
var receipts = context.Sent<SendInvoiceReceipt>();
var timeout = context.SingleScheduled<InvoicePaymentTimeout>();
```

`MiniBus.Testing` is intentionally not a processor harness or live integration testing package. It does not configure Azure Functions, Azure Service Bus, SQL persistence, Azure Storage, OpenTelemetry, or a test assertion framework.
