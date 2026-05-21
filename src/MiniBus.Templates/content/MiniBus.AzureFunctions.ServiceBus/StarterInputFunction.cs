using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using MiniBus.AzureFunctions.Processing;

namespace MiniBus.FunctionApp.Template;

public sealed class StarterInputFunction
{
    private readonly MiniBusProcessor _processor;

    public StarterInputFunction(MiniBusProcessor processor)
    {
        _processor = processor;
    }

    [Function("StarterInput")]
    public Task Run(
        [ServiceBusTrigger(StarterTopology.InputQueue, Connection = StarterTopology.ServiceBusConnectionSetting)]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions actions,
        CancellationToken cancellationToken)
    {
        return _processor.ProcessAsync(message, actions, cancellationToken);
    }
}
