using Azure.Messaging.ServiceBus;
using MiniBus.AzureServiceBus.Dispatching;

namespace MiniBus.Samples.FunctionApp;

public sealed class SampleServiceBusSender : IAzureServiceBusSender
{
    public Task SendAsync(
        string destination,
        ServiceBusMessage message,
        CancellationToken cancellationToken = default)
    {
        throw CreateConfigurationException(destination);
    }

    public Task<long> ScheduleAsync(
        string destination,
        ServiceBusMessage message,
        DateTimeOffset scheduledEnqueueTime,
        CancellationToken cancellationToken = default)
    {
        throw CreateConfigurationException(destination);
    }

    private static InvalidOperationException CreateConfigurationException(string destination)
    {
        return new InvalidOperationException(
            $"Sample transport sender is not connected to Azure Service Bus. Register AzureServiceBusSender with a ServiceBusClient to dispatch to '{destination}'.");
    }
}
