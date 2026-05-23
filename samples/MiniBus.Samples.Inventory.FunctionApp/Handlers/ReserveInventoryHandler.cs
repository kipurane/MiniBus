using Microsoft.Extensions.Logging;
using MiniBus.Core.Context;
using MiniBus.Core.Handlers;
using MiniBus.Samples.Contracts.Inventory;

namespace MiniBus.Samples.Inventory.FunctionApp.Handlers;

public sealed partial class ReserveInventoryHandler : IHandleMessages<ReserveInventory>
{
    private readonly InventoryReservationLog _reservations;
    private readonly ILogger<ReserveInventoryHandler> _logger;

    public ReserveInventoryHandler(
        InventoryReservationLog reservations,
        ILogger<ReserveInventoryHandler> logger)
    {
        _reservations = reservations;
        _logger = logger;
    }

    public Task Handle(
        ReserveInventory message,
        MiniBusContext context,
        CancellationToken cancellationToken)
    {
        _reservations.Record(new InventoryReservation(
            message.InvoiceId,
            message.CustomerId,
            message.Sku,
            message.Quantity));

        LogReserved(
            _logger,
            message.Quantity,
            message.Sku,
            message.InvoiceId,
            message.CustomerId,
            context.CorrelationId);

        return Task.CompletedTask;
    }

    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Information,
        Message = "Reserved {Quantity} of {Sku} for invoice {InvoiceId}, customer {CustomerId}, correlation {CorrelationId}.")]
    private static partial void LogReserved(
        ILogger logger,
        int quantity,
        string sku,
        string invoiceId,
        string customerId,
        string correlationId);
}
