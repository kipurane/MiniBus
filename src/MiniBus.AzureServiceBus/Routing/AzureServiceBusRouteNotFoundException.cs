namespace MiniBus.AzureServiceBus.Routing;

public sealed class AzureServiceBusRouteNotFoundException : InvalidOperationException
{
    public AzureServiceBusRouteNotFoundException(Type messageType, string operation)
        : base($"No Azure Service Bus route is configured for message type '{messageType.FullName}' and operation '{operation}'.")
    {
        MessageType = messageType;
        Operation = operation;
    }

    public Type MessageType { get; }

    public string Operation { get; }
}
