using Azure.Messaging.ServiceBus;
using MiniBus.AzureServiceBus.Dispatching;
using MiniBus.AzureServiceBus.Routing;
using MiniBus.AzureServiceBus.TransportMessageMapping;
using MiniBus.Core.Contracts;
using MiniBus.Core.Persistence;
using MiniBus.Core.Serialization;
using HeaderNames = MiniBus.Core.Headers.MiniBusHeaderNames;

namespace MiniBus.AzureServiceBus.IntegrationTests;

public sealed class LiveOutboxReplayIntegrationTests : LiveServiceBusTestBase
{
    [LiveServiceBusFact]
    public async Task ReplayedStoredOperation_UsesDeterministicMessageIdAndBrokerSuppressesDuplicate()
    {
        var scope = CreateScope("outbox-replay");
        var queue = await scope.CreateQueueAsync(requiresDuplicateDetection: true);
        await using var receiver = scope.CreateQueueReceiver(queue);
        await using var azureSender = new AzureServiceBusSender(scope.Client);
        var recordingSender = new RecordingForwardingSender(azureSender);
        var serializer = new SystemTextJsonMessageSerializer();
        var routes = new AzureServiceBusTransportRoutes();
        routes.MapCommand<ReplayCommand>(queue);
        var dispatcher = new AzureServiceBusTransportDispatcher(
            routes,
            new AzureServiceBusMessageFactory(serializer),
            recordingSender);
        var command = new ReplayCommand(Guid.NewGuid());
        var outgoingMessageId = $"outbox-{Guid.NewGuid():N}";
        var operation = new MiniBusOutboxStoredOperation(
            Guid.NewGuid(),
            outgoingMessageId,
            MiniBusOutboxOperationKind.Send,
            serializer.Serialize(command, typeof(ReplayCommand)),
            typeof(ReplayCommand),
            new Dictionary<string, string>
            {
                [HeaderNames.CorrelationId] = $"correlation-{Guid.NewGuid():N}"
            },
            DueTime: null,
            AttemptCount: 1);

        await dispatcher.DispatchAsync(operation);
        await dispatcher.DispatchAsync(operation with { AttemptCount = 2 });

        Assert.Equal(2, recordingSender.MessageIds.Count);
        Assert.All(recordingSender.MessageIds, messageId => Assert.Equal(outgoingMessageId, messageId));

        var received = await LiveServiceBusWait.ReceiveAsync(receiver, queue, TimeSpan.FromSeconds(30));
        try
        {
            Assert.Equal(outgoingMessageId, received.MessageId);
        }
        finally
        {
            await receiver.CompleteMessageAsync(received);
        }

        var duplicate = await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(5));
        Assert.Null(duplicate);
    }

    private sealed class RecordingForwardingSender : IAzureServiceBusSender
    {
        private readonly IAzureServiceBusSender _inner;

        public RecordingForwardingSender(IAzureServiceBusSender inner)
        {
            _inner = inner;
        }

        public List<string?> MessageIds { get; } = [];

        public Task SendAsync(
            string destination,
            ServiceBusMessage message,
            CancellationToken cancellationToken = default)
        {
            MessageIds.Add(message.MessageId);
            return _inner.SendAsync(destination, message, cancellationToken);
        }

        public Task<long> ScheduleAsync(
            string destination,
            ServiceBusMessage message,
            DateTimeOffset scheduledEnqueueTime,
            CancellationToken cancellationToken = default)
        {
            MessageIds.Add(message.MessageId);
            return _inner.ScheduleAsync(destination, message, scheduledEnqueueTime, cancellationToken);
        }
    }

    public sealed record ReplayCommand(Guid Id) : ICommand;
}
