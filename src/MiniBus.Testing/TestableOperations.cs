using MiniBus.Core.Contracts;

namespace MiniBus.Testing;

public abstract record TestableOutgoingOperation(object Message, Type MessageType)
{
    public TMessage GetMessage<TMessage>()
        where TMessage : IMessage
    {
        return (TMessage)Message;
    }
}

public sealed record TestableSentOperation(object Message, Type MessageType)
    : TestableOutgoingOperation(Message, MessageType);

public sealed record TestableSentOperation<TCommand>(TCommand Message, Type MessageType)
    where TCommand : ICommand;

public sealed record TestablePublishedOperation(object Message, Type MessageType)
    : TestableOutgoingOperation(Message, MessageType);

public sealed record TestablePublishedOperation<TEvent>(TEvent Message, Type MessageType)
    where TEvent : IEvent;

public sealed record TestableScheduledOperation(object Message, Type MessageType, DateTimeOffset DueTime)
    : TestableOutgoingOperation(Message, MessageType);

public sealed record TestableScheduledOperation<TMessage>(
    TMessage Message,
    Type MessageType,
    DateTimeOffset DueTime)
    where TMessage : IMessage;
