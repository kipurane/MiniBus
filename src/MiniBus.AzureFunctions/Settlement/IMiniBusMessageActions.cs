using Azure.Messaging.ServiceBus;

namespace MiniBus.AzureFunctions.Settlement;

public interface IMiniBusMessageActions
{
    Task CompleteMessageAsync(ServiceBusReceivedMessage message, CancellationToken cancellationToken = default);

    Task DeadLetterMessageAsync(
        ServiceBusReceivedMessage message,
        string deadLetterReason,
        string? deadLetterErrorDescription = null,
        CancellationToken cancellationToken = default);
}
