using Azure.Messaging.ServiceBus;

namespace MiniBus.AzureServiceBus.Recoverability;

public interface IAzureServiceBusDelayedRetryScheduler
{
    Task<long> ScheduleRetryAsync(
        ServiceBusReceivedMessage receivedMessage,
        Type messageType,
        DateTimeOffset scheduledEnqueueTime,
        IReadOnlyDictionary<string, string> headers,
        CancellationToken cancellationToken = default);
}
