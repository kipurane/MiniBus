using MiniBus.Core.Context;
using MiniBus.Core.Sagas;
using MiniBus.Samples.FunctionApp.Contracts;

namespace MiniBus.Samples.FunctionApp.Sagas;

public sealed class BillingSagaData : ISagaData
{
    public Guid Id { get; set; }

    public string CorrelationId { get; set; } = string.Empty;

    public bool IsCompleted { get; set; }

    public bool InvoiceCreated { get; set; }
}

public sealed class BillingSaga :
    MiniBusSaga<BillingSagaData>,
    IHandleSagaMessages<InvoiceCreated>
{
    public override void ConfigureHowToFindSaga(SagaMapper<BillingSagaData> mapper)
    {
        mapper.StartsWith<InvoiceCreated>(message => message.InvoiceId);
    }

    public Task Handle(InvoiceCreated message, MiniBusContext context, CancellationToken cancellationToken)
    {
        Data.InvoiceCreated = true;
        MarkAsComplete();
        return Task.CompletedTask;
    }
}
