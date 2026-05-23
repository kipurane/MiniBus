using MiniBus.Core.Contracts;
using MiniBus.Core.Sagas;

namespace MiniBus.Samples.Contracts.Billing;

public sealed record CreateInvoice(string InvoiceId, string CustomerId, decimal Amount) : ICommand;

public sealed record SendInvoiceReceipt(string InvoiceId, string CustomerId) : ICommand;

public sealed record InvoiceCreated(string InvoiceId, string CustomerId, decimal Amount) : IEvent;

public sealed record InvoicePaymentTimeout(string InvoiceId) : ISagaTimeout;
