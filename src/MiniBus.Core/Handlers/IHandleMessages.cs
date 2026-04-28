using MiniBus.Core.Context;
using MiniBus.Core.Contracts;

namespace MiniBus.Core.Handlers;

public interface IHandleMessages<in TMessage>
    where TMessage : IMessage
{
    Task Handle(TMessage message, MiniBusContext context, CancellationToken cancellationToken);
}

