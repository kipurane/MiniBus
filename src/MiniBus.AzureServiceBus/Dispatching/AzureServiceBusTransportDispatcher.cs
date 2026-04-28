using MiniBus.AzureServiceBus.Routing;
using MiniBus.AzureServiceBus.TransportMessageMapping;
using MiniBus.Core.Contracts;

namespace MiniBus.AzureServiceBus.Dispatching;

public sealed class AzureServiceBusTransportDispatcher
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

    public Task SendAsync<TCommand>(
        TCommand command,
        IReadOnlyDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
        where TCommand : ICommand
    {
        ArgumentNullException.ThrowIfNull(command);

        var messageType = typeof(TCommand);
        var destination = _routes.GetCommandQueue(messageType);
        var message = _messageFactory.CreateMessage(command, messageType, headers);

        return _sender.SendAsync(destination, message, cancellationToken);
    }

    public Task PublishAsync<TEvent>(
        TEvent @event,
        IReadOnlyDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
        where TEvent : IEvent
    {
        ArgumentNullException.ThrowIfNull(@event);

        var messageType = typeof(TEvent);
        var destination = _routes.GetEventTopic(messageType);
        var message = _messageFactory.CreateMessage(@event, messageType, headers);

        return _sender.SendAsync(destination, message, cancellationToken);
    }

    public Task<long> ScheduleAsync<TMessage>(
        TMessage message,
        DateTimeOffset dueTime,
        IReadOnlyDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
        where TMessage : IMessage
    {
        ArgumentNullException.ThrowIfNull(message);

        var messageType = typeof(TMessage);
        var destination = _routes.GetScheduledDestination(messageType);
        var serviceBusMessage = _messageFactory.CreateMessage(message, messageType, headers);

        return _sender.ScheduleAsync(destination, serviceBusMessage, dueTime, cancellationToken);
    }
}
