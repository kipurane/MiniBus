namespace MiniBus.AzureServiceBus.Routing;

public sealed class AzureServiceBusRouteConflictException : InvalidOperationException
{
    public AzureServiceBusRouteConflictException(Type messageType, string existingDestination, string requestedDestination)
        : base($"A route for message type '{messageType.FullName}' already exists for destination '{existingDestination}' and cannot be changed to '{requestedDestination}'.")
    {
        MessageType = messageType;
        ExistingDestination = existingDestination;
        RequestedDestination = requestedDestination;
    }

    public Type MessageType { get; }

    public string ExistingDestination { get; }

    public string RequestedDestination { get; }
}
