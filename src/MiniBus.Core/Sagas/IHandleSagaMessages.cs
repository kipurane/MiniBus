using MiniBus.Core.Context;
using MiniBus.Core.Contracts;

namespace MiniBus.Core.Sagas;

public interface IHandleSagaMessages<in TMessage>
    where TMessage : IMessage
{
    Task Handle(TMessage message, MiniBusContext context, CancellationToken cancellationToken);
}
