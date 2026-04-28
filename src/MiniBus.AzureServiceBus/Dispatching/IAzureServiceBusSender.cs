using Azure.Messaging.ServiceBus;

namespace MiniBus.AzureServiceBus.Dispatching;

public interface IAzureServiceBusSender
{
    Task SendAsync(string destination, ServiceBusMessage message, CancellationToken cancellationToken = default);

    Task<long> ScheduleAsync(
        string destination,
        ServiceBusMessage message,
        DateTimeOffset scheduledEnqueueTime,
        CancellationToken cancellationToken = default);
}
