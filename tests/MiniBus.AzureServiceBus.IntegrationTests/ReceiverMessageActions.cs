using Azure.Messaging.ServiceBus;
using MiniBus.AzureFunctions.Settlement;

namespace MiniBus.AzureServiceBus.IntegrationTests;

internal sealed class ReceiverMessageActions : IMiniBusMessageActions
{
    private readonly ServiceBusReceiver _receiver;
    private int _completedMessageCount;

    public ReceiverMessageActions(ServiceBusReceiver receiver)
    {
        _receiver = receiver;
    }

    public int CompletedMessageCount => Volatile.Read(ref _completedMessageCount);

    public async Task CompleteMessageAsync(
        ServiceBusReceivedMessage message,
        CancellationToken cancellationToken = default)
    {
        await _receiver.CompleteMessageAsync(message, cancellationToken).ConfigureAwait(false);
        Interlocked.Increment(ref _completedMessageCount);
    }

    public Task DeadLetterMessageAsync(
        ServiceBusReceivedMessage message,
        string deadLetterReason,
        string? deadLetterErrorDescription = null,
        CancellationToken cancellationToken = default)
    {
        return _receiver.DeadLetterMessageAsync(
            message,
            deadLetterReason,
            deadLetterErrorDescription,
            cancellationToken);
    }

    public Task AbandonMessageAsync(
        ServiceBusReceivedMessage message,
        CancellationToken cancellationToken = default)
    {
        return _receiver.AbandonMessageAsync(message, cancellationToken: cancellationToken);
    }
}
