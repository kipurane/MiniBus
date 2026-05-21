using Microsoft.Extensions.Logging;
using MiniBus.Core.Context;
using MiniBus.Core.Handlers;
using MiniBus.FunctionApp.Template.Contracts;

namespace MiniBus.FunctionApp.Template.Handlers;

public sealed partial class SubmitOrderHandler : IHandleMessages<SubmitOrder>
{
    private readonly ILogger<SubmitOrderHandler> _logger;

    public SubmitOrderHandler(ILogger<SubmitOrderHandler> logger)
    {
        _logger = logger;
    }

    public async Task Handle(
        SubmitOrder message,
        MiniBusContext context,
        CancellationToken cancellationToken)
    {
        LogSubmittingOrder(_logger, message.OrderId, message.CustomerId);

        await context.Publish(
                new OrderSubmitted(message.OrderId, message.CustomerId),
                cancellationToken)
            .ConfigureAwait(false);
    }

    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Information,
        Message = "Submitting order {OrderId} for customer {CustomerId}.")]
    private static partial void LogSubmittingOrder(
        ILogger logger,
        string orderId,
        string customerId);
}
