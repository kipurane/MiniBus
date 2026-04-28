using MiniBus.Core.Contracts;

namespace MiniBus.Core.Context;

public abstract class MiniBusContext
{
    public abstract string EndpointName { get; }

    public abstract string MessageId { get; }

    public abstract string CorrelationId { get; }

    public abstract string? CausationId { get; }

    public abstract IReadOnlyDictionary<string, string> Headers { get; }

    public abstract Task Send<TCommand>(
        TCommand command,
        CancellationToken cancellationToken = default)
        where TCommand : ICommand;

    public abstract Task Publish<TEvent>(
        TEvent @event,
        CancellationToken cancellationToken = default)
        where TEvent : IEvent;

    public abstract Task Schedule<TMessage>(
        TMessage message,
        DateTimeOffset dueTime,
        CancellationToken cancellationToken = default)
        where TMessage : IMessage;
}

