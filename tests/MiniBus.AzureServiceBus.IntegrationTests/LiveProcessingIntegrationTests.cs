using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MiniBus.AzureFunctions.DependencyInjection;
using MiniBus.AzureFunctions.Processing;
using MiniBus.AzureServiceBus.Dispatching;
using MiniBus.AzureServiceBus.Routing;
using MiniBus.AzureServiceBus.TransportMessageMapping;
using MiniBus.Core.Context;
using MiniBus.Core.Contracts;
using MiniBus.Core.Handlers;
using MiniBus.Core.Recoverability;
using MiniBus.Core.Serialization;
using HeaderNames = MiniBus.Core.Headers.MiniBusHeaderNames;

namespace MiniBus.AzureServiceBus.IntegrationTests;

public sealed class LiveProcessingIntegrationTests : LiveServiceBusTestBase
{
    private static readonly TimeSpan ReceiveTimeout = TimeSpan.FromSeconds(30);

    [LiveServiceBusFact]
    public async Task SuccessfulProcessing_CompletesLiveMessage()
    {
        var scope = CreateScope("processing-complete");
        var queue = await scope.CreateQueueAsync();
        await using var receiver = scope.CreateQueueReceiver(queue);
        await using var transportSender = new AzureServiceBusSender(scope.Client);
        var serializer = new SystemTextJsonMessageSerializer();
        var handler = new SuccessfulHandler();
        await using var provider = BuildProvider(serializer, transportSender, handler, immediateRetries: 0);
        await SendInputAsync(scope.Client, queue, serializer, new SuccessfulCommand(Guid.NewGuid()));
        var received = await LiveServiceBusWait.ReceiveAsync(receiver, queue, ReceiveTimeout);

        var actions = new ReceiverMessageActions(receiver);
        await provider.GetRequiredService<MiniBusProcessor>().ProcessAsync(received, actions);

        Assert.Equal(1, handler.InvocationCount);
        Assert.Equal(1, actions.CompletedMessageCount);
        Assert.Null(await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(3)));
    }

    [LiveServiceBusFact]
    public async Task AbandonedMessage_IsRedeliveredAndThenCompletedByMiniBus()
    {
        var scope = CreateScope("processing-abandon");
        var queue = await scope.CreateQueueAsync();
        await using var receiver = scope.CreateQueueReceiver(queue);
        await using var transportSender = new AzureServiceBusSender(scope.Client);
        var serializer = new SystemTextJsonMessageSerializer();
        var handler = new SuccessfulHandler();
        await using var provider = BuildProvider(serializer, transportSender, handler, immediateRetries: 0);
        await SendInputAsync(scope.Client, queue, serializer, new SuccessfulCommand(Guid.NewGuid()));
        var firstDelivery = await LiveServiceBusWait.ReceiveAsync(receiver, queue, ReceiveTimeout);
        var actions = new ReceiverMessageActions(receiver);

        await actions.AbandonMessageAsync(firstDelivery);

        var redelivered = await LiveServiceBusWait.ReceiveAsync(receiver, queue, ReceiveTimeout);
        Assert.True(redelivered.DeliveryCount > firstDelivery.DeliveryCount);

        await provider.GetRequiredService<MiniBusProcessor>().ProcessAsync(redelivered, actions);

        Assert.Equal(1, handler.InvocationCount);
        Assert.Equal(1, actions.CompletedMessageCount);
        Assert.Null(await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(3)));
    }

    [LiveServiceBusFact]
    public async Task ImmediateRetry_SucceedsAndCompletesWithoutDeadLettering()
    {
        var scope = CreateScope("processing-retry");
        var queue = await scope.CreateQueueAsync();
        await using var receiver = scope.CreateQueueReceiver(queue);
        await using var deadLetterReceiver = scope.CreateDeadLetterReceiver(queue);
        await using var transportSender = new AzureServiceBusSender(scope.Client);
        var serializer = new SystemTextJsonMessageSerializer();
        var handler = new FailOnceHandler();
        await using var provider = BuildProvider(serializer, transportSender, handler, immediateRetries: 1);
        await SendInputAsync(scope.Client, queue, serializer, new RetryCommand(Guid.NewGuid()));
        var received = await LiveServiceBusWait.ReceiveAsync(receiver, queue, ReceiveTimeout);

        var actions = new ReceiverMessageActions(receiver);
        await provider.GetRequiredService<MiniBusProcessor>().ProcessAsync(received, actions);

        Assert.Equal(2, handler.InvocationCount);
        Assert.Equal(1, actions.CompletedMessageCount);
        Assert.Null(await deadLetterReceiver.ReceiveMessageAsync(TimeSpan.FromSeconds(3)));
    }

    [LiveServiceBusFact]
    public async Task ExhaustedRetries_DeadLetterWithMiniBusDiagnostics()
    {
        var scope = CreateScope("processing-deadletter");
        var queue = await scope.CreateQueueAsync();
        await using var receiver = scope.CreateQueueReceiver(queue);
        await using var deadLetterReceiver = scope.CreateDeadLetterReceiver(queue);
        await using var transportSender = new AzureServiceBusSender(scope.Client);
        var serializer = new SystemTextJsonMessageSerializer();
        var handler = new PermanentlyFailingHandler();
        await using var provider = BuildProvider(serializer, transportSender, handler, immediateRetries: 1);
        await SendInputAsync(scope.Client, queue, serializer, new FailingCommand(Guid.NewGuid()));
        var received = await LiveServiceBusWait.ReceiveAsync(receiver, queue, ReceiveTimeout);

        await provider.GetRequiredService<MiniBusProcessor>()
            .ProcessAsync(received, new ReceiverMessageActions(receiver));

        Assert.Equal(2, handler.InvocationCount);
        var deadLettered = await LiveServiceBusWait.ReceiveAsync(
            deadLetterReceiver,
            $"{queue}/$deadletterqueue",
            ReceiveTimeout);
        try
        {
            Assert.Equal(
                RecoverabilityDecisionMaker.RetriesExhaustedDeadLetterReason,
                deadLettered.DeadLetterReason);
            Assert.Contains("permanent live failure", deadLettered.DeadLetterErrorDescription);
            Assert.DoesNotContain("SharedAccessKey", deadLettered.DeadLetterErrorDescription);
        }
        finally
        {
            await deadLetterReceiver.CompleteMessageAsync(deadLettered);
        }
    }

    private static ServiceProvider BuildProvider<TMessage>(
        IMessageSerializer serializer,
        IAzureServiceBusSender sender,
        IHandleMessages<TMessage> handler,
        int immediateRetries)
        where TMessage : IMessage
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
        services.AddSingleton(serializer);
        services.AddSingleton(sender);
        services.AddSingleton(new AzureServiceBusTransportRoutes());
        services.AddSingleton<IHandleMessages<TMessage>>(handler);
        services.AddMiniBusAzureFunctions(options =>
        {
            options.EndpointName = "LiveAzureServiceBus";
            options.Recoverability.ImmediateRetries = immediateRetries;
            options.Recoverability.DeadLetterAfterRetriesExhausted = true;
        });

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
    }

    private static async Task SendInputAsync<TMessage>(
        ServiceBusClient client,
        string queue,
        IMessageSerializer serializer,
        TMessage message)
        where TMessage : IMessage
    {
        var factory = new AzureServiceBusMessageFactory(serializer);
        var messageId = $"processing-{Guid.NewGuid():N}";
        var serviceBusMessage = factory.CreateMessage(
            message,
            typeof(TMessage),
            new Dictionary<string, string>
            {
                [HeaderNames.MessageId] = messageId,
                [HeaderNames.CorrelationId] = $"correlation-{Guid.NewGuid():N}",
                [HeaderNames.ContentType] = "application/json"
            });

        await using var sender = client.CreateSender(queue);
        await sender.SendMessageAsync(serviceBusMessage);
    }

    public sealed record SuccessfulCommand(Guid Id) : ICommand;

    public sealed record RetryCommand(Guid Id) : ICommand;

    public sealed record FailingCommand(Guid Id) : ICommand;

    private sealed class SuccessfulHandler : IHandleMessages<SuccessfulCommand>
    {
        public int InvocationCount { get; private set; }

        public Task Handle(
            SuccessfulCommand message,
            MiniBusContext context,
            CancellationToken cancellationToken)
        {
            InvocationCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FailOnceHandler : IHandleMessages<RetryCommand>
    {
        public int InvocationCount { get; private set; }

        public Task Handle(
            RetryCommand message,
            MiniBusContext context,
            CancellationToken cancellationToken)
        {
            InvocationCount++;
            return InvocationCount == 1
                ? Task.FromException(new InvalidOperationException("transient live failure"))
                : Task.CompletedTask;
        }
    }

    private sealed class PermanentlyFailingHandler : IHandleMessages<FailingCommand>
    {
        public int InvocationCount { get; private set; }

        public Task Handle(
            FailingCommand message,
            MiniBusContext context,
            CancellationToken cancellationToken)
        {
            InvocationCount++;
            return Task.FromException(new InvalidOperationException("permanent live failure"));
        }
    }
}
