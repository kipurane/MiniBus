using Microsoft.Extensions.Logging;
using MiniBus.Core.Context;
using MiniBus.Core.Handlers;
using MiniBus.Samples.FunctionApp.Contracts;

namespace MiniBus.Samples.FunctionApp.Handlers;

public sealed class CreateInvoiceHandler : IHandleMessages<CreateInvoice>
{
    private readonly ILogger<CreateInvoiceHandler> logger;

    public CreateInvoiceHandler(ILogger<CreateInvoiceHandler> logger)
    {
        this.logger = logger;
    }

    public async Task Handle(
        CreateInvoice message,
        MiniBusContext context,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Creating invoice {InvoiceId} for customer {CustomerId}.",
            message.InvoiceId,
            message.CustomerId);

        await context.Publish(
                new InvoiceCreated(message.InvoiceId, message.CustomerId, message.Amount),
                cancellationToken)
            .ConfigureAwait(false);

        await context.Send(
                new SendInvoiceReceipt(message.InvoiceId, message.CustomerId),
                cancellationToken)
            .ConfigureAwait(false);
    }
}
