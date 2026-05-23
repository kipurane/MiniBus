namespace MiniBus.Samples.Inventory.FunctionApp.Handlers;

public sealed class InventoryReservationLog
{
    private readonly object _gate = new();
    private readonly List<InventoryReservation> _reservations = new();

    public IReadOnlyList<InventoryReservation> Reservations
    {
        get
        {
            lock (_gate)
            {
                return _reservations.ToArray();
            }
        }
    }

    public void Record(InventoryReservation reservation)
    {
        lock (_gate)
        {
            _reservations.Add(reservation);
        }
    }
}

public sealed record InventoryReservation(
    string InvoiceId,
    string CustomerId,
    string Sku,
    int Quantity);
