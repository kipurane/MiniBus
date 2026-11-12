using MiniBus.Core.Identity;
using MiniBus.Samples.Contracts.Billing;
using MiniBus.Samples.Contracts.Inventory;

namespace MiniBus.Samples.Contracts;

public static class SampleMessageIdentities
{
    public static void Configure(MiniBusMessageIdentityRegistry identities)
    {
        identities.Map<CreateInvoice>("billing.create-invoice");
        identities.Map<SendInvoiceReceipt>("billing.send-invoice-receipt");
        identities.Map<InvoiceCreated>("billing.invoice-created");
        identities.Map<InvoicePaymentTimeout>("billing.invoice-payment-timeout");
        identities.Map<ReserveInventory>("inventory.reserve");
    }
}
