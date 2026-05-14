using MiniBus.AzureServiceBus.Recoverability;
using MiniBus.Core.Recoverability;

namespace MiniBus.AzureFunctions.Processing.Pipeline;

internal sealed class DelayedRetryScheduler
{
    private readonly IServiceProvider _serviceProvider;

    public DelayedRetryScheduler(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task ScheduleAsync(
        MiniBusProcessingContext context,
        RecoverabilityDecision decision,
        CancellationToken cancellationToken)
    {
        var scheduler = _serviceProvider.GetService(typeof(IAzureServiceBusDelayedRetryScheduler)) as IAzureServiceBusDelayedRetryScheduler
                        ?? throw new InvalidOperationException("Azure Service Bus delayed retry scheduling is not configured for MiniBus Azure Functions processing. Register MiniBus Azure Functions with AddMiniBusAzureFunctions, or register IAzureServiceBusDelayedRetryScheduler with AzureServiceBusDelayedRetryScheduler.");

        var messageType = MessageTypeResolutionBehavior.ResolveMessageType(context.Headers);
        var dueTime = DateTimeOffset.UtcNow.Add(decision.Delay ?? TimeSpan.Zero);

        await scheduler
            .ScheduleRetryAsync(context.Message, messageType, dueTime, context.Headers, cancellationToken)
            .ConfigureAwait(false);
    }
}
