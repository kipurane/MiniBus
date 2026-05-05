using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;

namespace MiniBus.AzureFunctions.Settlement;

public sealed class ServiceBusMessageActionsAdapter : IMiniBusMessageActions
{
    private readonly ServiceBusMessageActions _actions;

    public ServiceBusMessageActionsAdapter(ServiceBusMessageActions actions)
    {
        _actions = actions;
    }

    public Task CompleteMessageAsync(ServiceBusReceivedMessage message, CancellationToken cancellationToken = default)
    {
        return _actions.CompleteMessageAsync(message, cancellationToken);
    }

    public Task DeadLetterMessageAsync(
        ServiceBusReceivedMessage message,
        string deadLetterReason,
        string? deadLetterErrorDescription = null,
        CancellationToken cancellationToken = default)
    {
        return _actions.DeadLetterMessageAsync(
            message,
            propertiesToModify: null,
            deadLetterReason: deadLetterReason,
            deadLetterErrorDescription: deadLetterErrorDescription,
            cancellationToken: cancellationToken);
    }
}
