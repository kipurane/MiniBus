using MiniBus.Core.Context;
using MiniBus.Core.Sagas;
using MiniBus.Samples.Contracts.Billing;

namespace MiniBus.Samples.Billing.FunctionApp.Sagas;

public sealed class BillingSagaData : ISagaData
{
    public Guid Id { get; set; }

    public string CorrelationId { get; set; } = string.Empty;

    public bool IsCompleted { get; set; }

    public bool InvoiceCreated { get; set; }

    public bool PaymentTimedOut { get; set; }
}

public sealed class BillingSaga :
    MiniBusSaga<BillingSagaData>,
    IHandleSagaMessages<InvoiceCreated>,
    IHandleSagaMessages<InvoicePaymentTimeout>
{
    public override void ConfigureHowToFindSaga(SagaMapper<BillingSagaData> mapper)
    {
        mapper.StartsWith<InvoiceCreated>(message => message.InvoiceId)
            .Correlate<InvoicePaymentTimeout>(message => message.InvoiceId);
    }

    public async Task Handle(InvoiceCreated message, MiniBusContext context, CancellationToken cancellationToken)
    {
        Data.InvoiceCreated = true;
        await RequestTimeout(
                new InvoicePaymentTimeout(message.InvoiceId),
                TimeSpan.FromDays(7),
                context,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public Task Handle(InvoicePaymentTimeout message, MiniBusContext context, CancellationToken cancellationToken)
    {
        Data.PaymentTimedOut = true;
        MarkAsComplete();
        return Task.CompletedTask;
    }
}
