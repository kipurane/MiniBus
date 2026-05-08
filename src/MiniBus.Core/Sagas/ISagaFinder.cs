using MiniBus.Core.Context;
using MiniBus.Core.Contracts;

namespace MiniBus.Core.Sagas;

public interface ISagaFinder<in TMessage, TData>
    where TMessage : IMessage
    where TData : class, ISagaData, new()
{
    Task<string?> FindCorrelationId(TMessage message, MiniBusContext context, CancellationToken cancellationToken);
}
