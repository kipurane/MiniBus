using MiniBus.Core.Contracts;

namespace MiniBus.Samples.FunctionApp.Contracts;

public sealed record CreateInvoice(string InvoiceId, string CustomerId, decimal Amount) : ICommand;

public sealed record SendInvoiceReceipt(string InvoiceId, string CustomerId) : ICommand;

public sealed record InvoiceCreated(string InvoiceId, string CustomerId, decimal Amount) : IEvent;
