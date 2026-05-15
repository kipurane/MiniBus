using MiniBus.AzureServiceBus.Routing;
using MiniBus.AzureServiceBus.TransportMessageMapping;
using MiniBus.Core.Contracts;
using MiniBus.Core.Persistence;

namespace MiniBus.AzureServiceBus.Dispatching;

public sealed class AzureServiceBusTransportDispatcher : IMiniBusOutboxDispatcher
{
    private readonly AzureServiceBusTransportRoutes _routes;
    private readonly AzureServiceBusMessageFactory _messageFactory;
    private readonly IAzureServiceBusSender _sender;

    public AzureServiceBusTransportDispatcher(
        AzureServiceBusTransportRoutes routes,
        AzureServiceBusMessageFactory messageFactory,
        IAzureServiceBusSender sender)
    {
        _routes = routes;
        _messageFactory = messageFactory;
        _sender = sender;
    }

    public async Task SendAsync<TCommand>(
        TCommand command,
        IReadOnlyDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
        where TCommand : ICommand
    {
        ArgumentNullException.ThrowIfNull(command);

        var messageType = typeof(TCommand);
        var destination = _routes.GetCommandQueue(messageType);
        var message = await _messageFactory
            .CreateMessageAsync(command, messageType, headers, cancellationToken)
            .ConfigureAwait(false);

        await _sender.SendAsync(destination, message, cancellationToken).ConfigureAwait(false);
    }

    public async Task PublishAsync<TEvent>(
        TEvent @event,
        IReadOnlyDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
        where TEvent : IEvent
    {
        ArgumentNullException.ThrowIfNull(@event);

        var messageType = typeof(TEvent);
        var destination = _routes.GetEventTopic(messageType);
        var message = await _messageFactory
            .CreateMessageAsync(@event, messageType, headers, cancellationToken)
            .ConfigureAwait(false);

        await _sender.SendAsync(destination, message, cancellationToken).ConfigureAwait(false);
    }

    public async Task<long> ScheduleAsync<TMessage>(
        TMessage message,
        DateTimeOffset dueTime,
        IReadOnlyDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
        where TMessage : IMessage
    {
        ArgumentNullException.ThrowIfNull(message);

        var messageType = typeof(TMessage);
        var destination = _routes.GetScheduledDestination(messageType);
        var serviceBusMessage = await _messageFactory
            .CreateMessageAsync(message, messageType, headers, cancellationToken)
            .ConfigureAwait(false);

        return await _sender
            .ScheduleAsync(destination, serviceBusMessage, dueTime, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task DispatchAsync(
        MiniBusOutboxStoredOperation operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        var destination = operation.Kind switch
        {
            MiniBusOutboxOperationKind.Send => _routes.GetCommandQueue(operation.MessageType),
            MiniBusOutboxOperationKind.Publish => _routes.GetEventTopic(operation.MessageType),
            MiniBusOutboxOperationKind.Schedule => _routes.GetScheduledDestination(operation.MessageType),
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation.Kind, "Unsupported MiniBus outbox operation kind.")
        };

        var headers = new Dictionary<string, string>(operation.Headers, StringComparer.Ordinal)
        {
            [TransportMessageMapping.MiniBusHeaderNames.MessageId] = operation.OutgoingMessageId
        };
        var message = _messageFactory.CreateMessage(operation.Body, operation.MessageType, headers);

        if (operation.Kind == MiniBusOutboxOperationKind.Schedule)
        {
            await _sender
                .ScheduleAsync(
                    destination,
                    message,
                    operation.DueTime ?? throw new InvalidOperationException("Scheduled outbox operations require a due time."),
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        await _sender.SendAsync(destination, message, cancellationToken).ConfigureAwait(false);
    }
}
