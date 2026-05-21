using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using MiniBus.AzureServiceBus.Dispatching;

namespace MiniBus.Samples.FunctionApp;

public sealed partial class BillingSampleServiceBusSender : IAzureServiceBusSender, IAsyncDisposable
{
    private readonly AzureServiceBusSender _sender;
    private readonly ILogger<BillingSampleServiceBusSender> _logger;

    public BillingSampleServiceBusSender(
        ServiceBusClient client,
        ILogger<BillingSampleServiceBusSender> logger)
    {
        _sender = new AzureServiceBusSender(client);
        _logger = logger;
    }

    public async Task SendAsync(
        string destination,
        ServiceBusMessage message,
        CancellationToken cancellationToken = default)
    {
        await _sender.SendAsync(destination, message, cancellationToken).ConfigureAwait(false);
        LogSent(_logger, message.MessageId, destination);
    }

    public async Task<long> ScheduleAsync(
        string destination,
        ServiceBusMessage message,
        DateTimeOffset scheduledEnqueueTime,
        CancellationToken cancellationToken = default)
    {
        var sequenceNumber = await _sender
            .ScheduleAsync(destination, message, scheduledEnqueueTime, cancellationToken)
            .ConfigureAwait(false);

        LogScheduled(_logger, message.MessageId, destination, scheduledEnqueueTime);
        return sequenceNumber;
    }

    public ValueTask DisposeAsync()
    {
        return _sender.DisposeAsync();
    }

    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Information,
        Message = "Billing sample sent Service Bus message {MessageId} to {Destination}.")]
    private static partial void LogSent(
        ILogger logger,
        string messageId,
        string destination);

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Billing sample scheduled Service Bus message {MessageId} to {Destination} for {ScheduledEnqueueTime}.")]
    private static partial void LogScheduled(
        ILogger logger,
        string messageId,
        string destination,
        DateTimeOffset scheduledEnqueueTime);
}
