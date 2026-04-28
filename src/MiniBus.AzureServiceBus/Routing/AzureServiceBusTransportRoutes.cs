using MiniBus.Core.Contracts;

namespace MiniBus.AzureServiceBus.Routing;

public sealed class AzureServiceBusTransportRoutes
{
    private readonly Dictionary<Type, string> _commandQueues = new();
    private readonly Dictionary<Type, string> _eventTopics = new();
    private readonly Dictionary<Type, string> _scheduledDestinations = new();

    public void MapCommand<TCommand>(string queue)
        where TCommand : ICommand
    {
        MapCommand(typeof(TCommand), queue);
    }

    public void MapCommand(Type commandType, string queue)
    {
        ValidateMessageType<ICommand>(commandType, nameof(commandType));
        AddRoute(_commandQueues, commandType, queue);
    }

    public void MapEvent<TEvent>(string topic)
        where TEvent : IEvent
    {
        MapEvent(typeof(TEvent), topic);
    }

    public void MapEvent(Type eventType, string topic)
    {
        ValidateMessageType<IEvent>(eventType, nameof(eventType));
        AddRoute(_eventTopics, eventType, topic);
    }

    public void MapScheduledMessage<TMessage>(string destination)
        where TMessage : IMessage
    {
        MapScheduledMessage(typeof(TMessage), destination);
    }

    public void MapScheduledMessage(Type messageType, string destination)
    {
        ValidateMessageType<IMessage>(messageType, nameof(messageType));
        AddRoute(_scheduledDestinations, messageType, destination);
    }

    public string GetCommandQueue(Type commandType)
    {
        ValidateMessageType<ICommand>(commandType, nameof(commandType));
        return GetRoute(_commandQueues, commandType, "send");
    }

    public string GetEventTopic(Type eventType)
    {
        ValidateMessageType<IEvent>(eventType, nameof(eventType));
        return GetRoute(_eventTopics, eventType, "publish");
    }

    public string GetScheduledDestination(Type messageType)
    {
        ValidateMessageType<IMessage>(messageType, nameof(messageType));

        if (typeof(ICommand).IsAssignableFrom(messageType) && _commandQueues.TryGetValue(messageType, out var queue))
        {
            return queue;
        }

        if (typeof(IEvent).IsAssignableFrom(messageType) && _eventTopics.TryGetValue(messageType, out var topic))
        {
            return topic;
        }

        return GetRoute(_scheduledDestinations, messageType, "schedule");
    }

    private static void AddRoute(Dictionary<Type, string> routes, Type messageType, string destination)
    {
        if (string.IsNullOrWhiteSpace(destination))
        {
            throw new ArgumentException("Destination must be provided.", nameof(destination));
        }

        if (routes.TryGetValue(messageType, out var existingDestination))
        {
            if (!string.Equals(existingDestination, destination, StringComparison.Ordinal))
            {
                throw new AzureServiceBusRouteConflictException(messageType, existingDestination, destination);
            }

            return;
        }

        routes.Add(messageType, destination);
    }

    private static string GetRoute(Dictionary<Type, string> routes, Type messageType, string operation)
    {
        if (routes.TryGetValue(messageType, out var destination))
        {
            return destination;
        }

        throw new AzureServiceBusRouteNotFoundException(messageType, operation);
    }

    private static void ValidateMessageType<TContract>(Type messageType, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(messageType);

        if (!typeof(TContract).IsAssignableFrom(messageType))
        {
            throw new ArgumentException($"Type '{messageType.FullName}' must implement {typeof(TContract).FullName}.", parameterName);
        }
    }
}
