using MiniBus.Core.Context;
using MiniBus.Core.Contracts;

namespace MiniBus.Testing;

public sealed class TestableMiniBusContext : MiniBusContext
{
    private readonly string _endpointName;
    private readonly string _messageId;
    private readonly string _correlationId;
    private readonly string? _causationId;
    private readonly Dictionary<string, string> _headers;
    private readonly List<TestableSentOperation> _sentMessages = new();
    private readonly List<TestablePublishedOperation> _publishedMessages = new();
    private readonly List<TestableScheduledOperation> _scheduledMessages = new();

    public TestableMiniBusContext(
        string endpointName = "Tests",
        string messageId = "message-id",
        string correlationId = "correlation-id",
        string? causationId = null,
        IReadOnlyDictionary<string, string>? headers = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpointName);
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        _endpointName = endpointName;
        _messageId = messageId;
        _correlationId = correlationId;
        _causationId = causationId;
        _headers = headers is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(headers, StringComparer.Ordinal);
    }

    public override string EndpointName => _endpointName;

    public override string MessageId => _messageId;

    public override string CorrelationId => _correlationId;

    public override string? CausationId => _causationId;

    public override IReadOnlyDictionary<string, string> Headers => _headers;

    public IDictionary<string, string> HeaderValues => _headers;

    public IReadOnlyList<TestableSentOperation> SentMessages => _sentMessages;

    public IReadOnlyList<TestablePublishedOperation> PublishedMessages => _publishedMessages;

    public IReadOnlyList<TestableScheduledOperation> ScheduledMessages => _scheduledMessages;

    public override Task Send<TCommand>(
        TCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        _sentMessages.Add(new TestableSentOperation(command, typeof(TCommand)));
        return Task.CompletedTask;
    }

    public override Task Publish<TEvent>(
        TEvent @event,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(@event);

        _publishedMessages.Add(new TestablePublishedOperation(@event, typeof(TEvent)));
        return Task.CompletedTask;
    }

    public override Task Schedule<TMessage>(
        TMessage message,
        DateTimeOffset dueTime,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        _scheduledMessages.Add(new TestableScheduledOperation(message, typeof(TMessage), dueTime));
        return Task.CompletedTask;
    }

    public IReadOnlyList<TestableSentOperation<TCommand>> Sent<TCommand>()
        where TCommand : ICommand
    {
        return _sentMessages
            .Where(operation => typeof(TCommand).IsAssignableFrom(operation.MessageType))
            .Select(operation => new TestableSentOperation<TCommand>(
                (TCommand)operation.Message,
                operation.MessageType))
            .ToArray();
    }

    public TestableSentOperation<TCommand> SingleSent<TCommand>()
        where TCommand : ICommand
    {
        return Single(Sent<TCommand>(), $"sent command of type '{typeof(TCommand).FullName}'");
    }

    public IReadOnlyList<TestablePublishedOperation<TEvent>> Published<TEvent>()
        where TEvent : IEvent
    {
        return _publishedMessages
            .Where(operation => typeof(TEvent).IsAssignableFrom(operation.MessageType))
            .Select(operation => new TestablePublishedOperation<TEvent>(
                (TEvent)operation.Message,
                operation.MessageType))
            .ToArray();
    }

    public TestablePublishedOperation<TEvent> SinglePublished<TEvent>()
        where TEvent : IEvent
    {
        return Single(Published<TEvent>(), $"published event of type '{typeof(TEvent).FullName}'");
    }

    public IReadOnlyList<TestableScheduledOperation<TMessage>> Scheduled<TMessage>()
        where TMessage : IMessage
    {
        return _scheduledMessages
            .Where(operation => typeof(TMessage).IsAssignableFrom(operation.MessageType))
            .Select(operation => new TestableScheduledOperation<TMessage>(
                (TMessage)operation.Message,
                operation.MessageType,
                operation.DueTime))
            .ToArray();
    }

    public TestableScheduledOperation<TMessage> SingleScheduled<TMessage>()
        where TMessage : IMessage
    {
        return Single(Scheduled<TMessage>(), $"scheduled message of type '{typeof(TMessage).FullName}'");
    }

    private static TOperation Single<TOperation>(
        IReadOnlyList<TOperation> operations,
        string description)
    {
        return operations.Count switch
        {
            1 => operations[0],
            0 => throw new InvalidOperationException($"Expected one {description}, but none were captured."),
            _ => throw new InvalidOperationException($"Expected one {description}, but {operations.Count} were captured.")
        };
    }
}
