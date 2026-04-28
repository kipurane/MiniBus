using System.Collections.Concurrent;
using Azure.Messaging.ServiceBus;

namespace MiniBus.AzureServiceBus.Dispatching;

public sealed class AzureServiceBusSender : IAzureServiceBusSender, IAsyncDisposable
{
    private readonly ServiceBusClient _client;
    private readonly ConcurrentDictionary<string, ServiceBusSender> _senders = new(StringComparer.Ordinal);

    public AzureServiceBusSender(ServiceBusClient client)
    {
        _client = client;
    }

    public Task SendAsync(string destination, ServiceBusMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);
        ArgumentNullException.ThrowIfNull(message);

        return GetSender(destination).SendMessageAsync(message, cancellationToken);
    }

    public Task<long> ScheduleAsync(
        string destination,
        ServiceBusMessage message,
        DateTimeOffset scheduledEnqueueTime,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);
        ArgumentNullException.ThrowIfNull(message);

        return GetSender(destination).ScheduleMessageAsync(message, scheduledEnqueueTime, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var sender in _senders.Values)
        {
            await sender.DisposeAsync().ConfigureAwait(false);
        }
    }

    private ServiceBusSender GetSender(string destination)
    {
        return _senders.GetOrAdd(destination, _client.CreateSender);
    }
}
