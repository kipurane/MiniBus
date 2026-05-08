using MiniBus.Core.Context;
using MiniBus.Core.Contracts;
using MiniBus.Core.Sagas;

namespace MiniBus.Samples.FunctionApp.Sagas;

public sealed record CreateInvoice(string InvoiceId) : ICommand;

public sealed class BillingSagaData : ISagaData
{
    public Guid Id { get; set; }

    public string CorrelationId { get; set; } = string.Empty;

    public bool IsCompleted { get; set; }

    public bool InvoiceCreated { get; set; }
}

public sealed class BillingSaga :
    MiniBusSaga<BillingSagaData>,
    IHandleSagaMessages<CreateInvoice>
{
    public override void ConfigureHowToFindSaga(SagaMapper<BillingSagaData> mapper)
    {
        mapper.StartsWith<CreateInvoice>(message => message.InvoiceId);
    }

    public Task Handle(CreateInvoice message, MiniBusContext context, CancellationToken cancellationToken)
    {
        Data.InvoiceCreated = true;
        MarkAsComplete();
        return Task.CompletedTask;
    }
}
