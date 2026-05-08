namespace MiniBus.AzureServiceBus.Recoverability;

public sealed class AzureServiceBusDelayedRetryDestinationException : InvalidOperationException
{
    public AzureServiceBusDelayedRetryDestinationException(Type messageType, Exception innerException)
        : base($"Unable to resolve an Azure Service Bus delayed retry destination for message type '{messageType.FullName}'. Configure a command route, event route, or scheduled message route for this message type before delayed retries can be scheduled.", innerException)
    {
        MessageType = messageType;
    }

    public Type MessageType { get; }
}
