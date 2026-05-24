using Microsoft.Extensions.Logging;
using MiniBus.Core.Context;
using MiniBus.Core.Handlers;
using MiniBus.Samples.Contracts.Billing;
using MiniBus.Samples.Contracts.Inventory;

namespace MiniBus.Samples.Billing.FunctionApp.Handlers;

public sealed partial class CreateInvoiceHandler : IHandleMessages<CreateInvoice>
{
    private readonly ILogger<CreateInvoiceHandler> _logger;

    public CreateInvoiceHandler(ILogger<CreateInvoiceHandler> logger)
    {
        _logger = logger;
    }

    public async Task Handle(
        CreateInvoice message,
        MiniBusContext context,
        CancellationToken cancellationToken)
    {
        LogCreatingInvoice(_logger, message.InvoiceId, message.CustomerId);

        await context.Publish(
                new InvoiceCreated(message.InvoiceId, message.CustomerId, message.Amount),
                cancellationToken)
            .ConfigureAwait(false);

        await context.Send(
                new ReserveInventory(message.InvoiceId, message.CustomerId, "sample-sku", 1),
                cancellationToken)
            .ConfigureAwait(false);

        await context.Send(
                new SendInvoiceReceipt(message.InvoiceId, message.CustomerId),
                cancellationToken)
            .ConfigureAwait(false);
    }

    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Information,
        Message = "Creating invoice {InvoiceId} for customer {CustomerId}.")]
    private static partial void LogCreatingInvoice(
        ILogger logger,
        string invoiceId,
        string customerId);
}
