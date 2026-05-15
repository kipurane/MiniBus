using MiniBus.Core.Context;
using MiniBus.Core.Contracts;
using MiniBus.AzureServiceBus.Dispatching;
using MiniBus.AzureServiceBus.TransportMessageMapping;
using MiniBus.Core.Persistence;

namespace MiniBus.AzureFunctions.Processing;

internal sealed class MiniBusReceivedMessageContext : MiniBusContext
{
    private readonly IReadOnlyDictionary<string, string> _headers;
    private readonly IServiceProvider _serviceProvider;
    private readonly MiniBusOutboxOperationCollector? _outboxCollector;

    public MiniBusReceivedMessageContext(
        string endpointName,
        string messageId,
        string correlationId,
        string? causationId,
        IReadOnlyDictionary<string, string> headers,
        IServiceProvider serviceProvider,
        MiniBusOutboxOperationCollector? outboxCollector = null)
    {
        EndpointName = endpointName;
        MessageId = messageId;
        CorrelationId = correlationId;
        CausationId = causationId;
        _headers = headers;
        _serviceProvider = serviceProvider;
        _outboxCollector = outboxCollector;
    }

    public override string EndpointName { get; }

    public override string MessageId { get; }

    public override string CorrelationId { get; }

    public override string? CausationId { get; }

    public override IReadOnlyDictionary<string, string> Headers => _headers;

    public override Task Send<TCommand>(TCommand command, CancellationToken cancellationToken = default)
    {
        if (_outboxCollector is not null)
        {
            _outboxCollector.Add(new MiniBusOutboxOperation(
                MiniBusOutboxOperationKind.Send,
                command,
                typeof(TCommand),
                CreateOutgoingHeaders(),
                DueTime: null));

            return Task.CompletedTask;
        }

        return GetDispatcher().SendAsync(command, CreateOutgoingHeaders(), cancellationToken);
    }

    public override Task Publish<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
    {
        if (_outboxCollector is not null)
        {
            _outboxCollector.Add(new MiniBusOutboxOperation(
                MiniBusOutboxOperationKind.Publish,
                @event,
                typeof(TEvent),
                CreateOutgoingHeaders(),
                DueTime: null));

            return Task.CompletedTask;
        }

        return GetDispatcher().PublishAsync(@event, CreateOutgoingHeaders(), cancellationToken);
    }

    public override async Task Schedule<TMessage>(TMessage message, DateTimeOffset dueTime, CancellationToken cancellationToken = default)
    {
        if (_outboxCollector is not null)
        {
            _outboxCollector.Add(new MiniBusOutboxOperation(
                MiniBusOutboxOperationKind.Schedule,
                message,
                typeof(TMessage),
                CreateOutgoingHeaders(),
                dueTime));

            return;
        }

        await GetDispatcher()
            .ScheduleAsync(message, dueTime, CreateOutgoingHeaders(), cancellationToken)
            .ConfigureAwait(false);
    }

    private AzureServiceBusTransportDispatcher GetDispatcher()
    {
        return _serviceProvider.GetService(typeof(AzureServiceBusTransportDispatcher)) as AzureServiceBusTransportDispatcher
               ?? throw new InvalidOperationException("Azure Service Bus transport dispatch is not configured for MiniBus Azure Functions processing.");
    }

    private IReadOnlyDictionary<string, string> CreateOutgoingHeaders()
    {
        var outgoingHeaders = new Dictionary<string, string>(_headers, StringComparer.Ordinal)
        {
            [MiniBusHeaderNames.CorrelationId] = CorrelationId,
            [MiniBusHeaderNames.CausationId] = MessageId
        };
        // Outgoing operations must not reuse receive-side type metadata; the transport
        // assigns these headers from the actual outgoing message type.
        outgoingHeaders.Remove(MiniBusHeaderNames.MessageType);
        outgoingHeaders.Remove(MiniBusHeaderNames.EnclosedMessageTypes);
        outgoingHeaders.Remove(MiniBusHeaderNames.ContentType);

        return outgoingHeaders;
    }
}
