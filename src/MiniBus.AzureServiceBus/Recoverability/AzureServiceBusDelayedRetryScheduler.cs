using Azure.Messaging.ServiceBus;
using MiniBus.AzureServiceBus.Dispatching;
using MiniBus.AzureServiceBus.Routing;
using MiniBus.AzureServiceBus.TransportMessageMapping;
using MiniBus.Core.Recoverability;

namespace MiniBus.AzureServiceBus.Recoverability;

public sealed class AzureServiceBusDelayedRetryScheduler : IAzureServiceBusDelayedRetryScheduler
{
    private readonly AzureServiceBusTransportRoutes _routes;
    private readonly IAzureServiceBusSender _sender;

    public AzureServiceBusDelayedRetryScheduler(
        AzureServiceBusTransportRoutes routes,
        IAzureServiceBusSender sender)
    {
        _routes = routes;
        _sender = sender;
    }

    public Task<long> ScheduleRetryAsync(
        ServiceBusReceivedMessage receivedMessage,
        Type messageType,
        DateTimeOffset scheduledEnqueueTime,
        IReadOnlyDictionary<string, string> headers,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(receivedMessage);
        ArgumentNullException.ThrowIfNull(messageType);
        ArgumentNullException.ThrowIfNull(headers);

        var destination = GetRetryDestination(messageType);
        var retryMessage = CreateRetryMessage(receivedMessage, headers);

        return _sender.ScheduleAsync(destination, retryMessage, scheduledEnqueueTime, cancellationToken);
    }

    private string GetRetryDestination(Type messageType)
    {
        try
        {
            var destination = _routes.GetScheduledDestination(messageType);

            if (string.IsNullOrWhiteSpace(destination))
            {
                throw new InvalidOperationException("The resolved delayed retry destination was empty.");
            }

            return destination;
        }
        catch (Exception exception)
        {
            throw new AzureServiceBusDelayedRetryDestinationException(messageType, exception);
        }
    }

    private static ServiceBusMessage CreateRetryMessage(
        ServiceBusReceivedMessage receivedMessage,
        IReadOnlyDictionary<string, string> headers)
    {
        var retryHeaders = new Dictionary<string, string>(headers, StringComparer.Ordinal);

        if (!retryHeaders.ContainsKey(MiniBusHeaderNames.MessageId)
            && !string.IsNullOrWhiteSpace(receivedMessage.MessageId))
        {
            retryHeaders[MiniBusHeaderNames.MessageId] = receivedMessage.MessageId;
        }

        if (!retryHeaders.ContainsKey(MiniBusHeaderNames.CorrelationId)
            && !string.IsNullOrWhiteSpace(receivedMessage.CorrelationId))
        {
            retryHeaders[MiniBusHeaderNames.CorrelationId] = receivedMessage.CorrelationId;
        }

        if (!retryHeaders.ContainsKey(MiniBusHeaderNames.ContentType)
            && !string.IsNullOrWhiteSpace(receivedMessage.ContentType))
        {
            retryHeaders[MiniBusHeaderNames.ContentType] = receivedMessage.ContentType;
        }

        if (!retryHeaders.ContainsKey(MiniBusRecoverabilityHeaderNames.OriginalMessageId)
            && !string.IsNullOrWhiteSpace(receivedMessage.MessageId))
        {
            retryHeaders[MiniBusRecoverabilityHeaderNames.OriginalMessageId] = receivedMessage.MessageId;
        }

        var retryMessage = new ServiceBusMessage(receivedMessage.Body)
        {
            MessageId = retryHeaders.GetValueOrDefault(MiniBusHeaderNames.MessageId, receivedMessage.MessageId),
            CorrelationId = retryHeaders.GetValueOrDefault(MiniBusHeaderNames.CorrelationId, receivedMessage.CorrelationId),
            ContentType = retryHeaders.GetValueOrDefault(MiniBusHeaderNames.ContentType, receivedMessage.ContentType),
            Subject = retryHeaders.GetValueOrDefault(MiniBusHeaderNames.MessageType, receivedMessage.Subject)
        };

        AzureServiceBusHeaderMapper.ApplyHeaders(retryMessage, retryHeaders);

        return retryMessage;
    }
}
