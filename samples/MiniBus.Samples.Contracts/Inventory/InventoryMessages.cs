using MiniBus.Core.Contracts;

namespace MiniBus.Samples.Contracts.Inventory;

public sealed record ReserveInventory(
    string InvoiceId,
    string CustomerId,
    string Sku,
    int Quantity) : ICommand;
