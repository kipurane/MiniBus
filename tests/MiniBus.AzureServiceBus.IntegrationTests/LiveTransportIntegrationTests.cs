using Azure.Messaging.ServiceBus;
using MiniBus.AzureServiceBus.Dispatching;
using MiniBus.AzureServiceBus.Routing;
using MiniBus.AzureServiceBus.TransportMessageMapping;
using MiniBus.Core.Contracts;
using MiniBus.Core.Serialization;
using HeaderNames = MiniBus.Core.Headers.MiniBusHeaderNames;

namespace MiniBus.AzureServiceBus.IntegrationTests;

public sealed class LiveTransportIntegrationTests : LiveServiceBusTestBase
{
    private static readonly TimeSpan ReceiveTimeout = TimeSpan.FromSeconds(30);

    [LiveServiceBusFact]
    public async Task RoutedCommand_RoundTripsBodyAndMetadataThroughLiveQueue()
    {
        var scope = CreateScope("routed-command");
        var queue = await scope.CreateQueueAsync();
        await using var receiver = scope.CreateQueueReceiver(queue);
        await using var sender = new AzureServiceBusSender(scope.Client);
        var serializer = new SystemTextJsonMessageSerializer();
        var dispatcher = CreateDispatcher(serializer, sender, routes => routes.MapCommand<LiveCommand>(queue));
        var command = new LiveCommand(Guid.NewGuid(), "command payload");
        var headers = CreateHeaders("command");

        await dispatcher.SendAsync(command, headers);

        var received = await LiveServiceBusWait.ReceiveAsync(receiver, queue, ReceiveTimeout);
        try
        {
            var deserialized = Assert.IsType<LiveCommand>(
                serializer.Deserialize(received.Body, typeof(LiveCommand)));
            Assert.Equal(command, deserialized);
            AssertMetadata(received, headers);

            var mappedHeaders = AzureServiceBusHeaderMapper.ReadHeaders(received);
            Assert.Equal(headers[HeaderNames.MessageId], mappedHeaders[HeaderNames.MessageId]);
            Assert.Equal(headers[HeaderNames.CorrelationId], mappedHeaders[HeaderNames.CorrelationId]);
            Assert.Equal(headers[HeaderNames.CausationId], mappedHeaders[HeaderNames.CausationId]);
            Assert.Equal(headers["MiniBus.Live.Custom"], mappedHeaders["MiniBus.Live.Custom"]);
        }
        finally
        {
            await receiver.CompleteMessageAsync(received);
        }
    }

    [LiveServiceBusFact]
    public async Task RoutedEvent_RoundTripsBodyAndMetadataThroughLiveSubscription()
    {
        var scope = CreateScope("routed-event");
        var entity = await scope.CreateTopicSubscriptionAsync();
        await using var receiver = scope.CreateSubscriptionReceiver(entity);
        await using var sender = new AzureServiceBusSender(scope.Client);
        var serializer = new SystemTextJsonMessageSerializer();
        var dispatcher = CreateDispatcher(serializer, sender, routes => routes.MapEvent<LiveEvent>(entity.Topic));
        var @event = new LiveEvent(Guid.NewGuid(), "event payload");
        var headers = CreateHeaders("event");

        await dispatcher.PublishAsync(@event, headers);

        var received = await LiveServiceBusWait.ReceiveAsync(receiver, entity.Alias, ReceiveTimeout);
        try
        {
            var deserialized = Assert.IsType<LiveEvent>(
                serializer.Deserialize(received.Body, typeof(LiveEvent)));
            Assert.Equal(@event, deserialized);
            AssertMetadata(received, headers);
        }
        finally
        {
            await receiver.CompleteMessageAsync(received);
        }
    }

    [LiveServiceBusFact]
    public async Task ScheduledCommand_IsNotAvailableEarlyAndPreservesBodyAndMetadata()
    {
        var scope = CreateScope("scheduled-command");
        var queue = await scope.CreateQueueAsync();
        await using var receiver = scope.CreateQueueReceiver(queue);
        await using var sender = new AzureServiceBusSender(scope.Client);
        var serializer = new SystemTextJsonMessageSerializer();
        var dispatcher = CreateDispatcher(serializer, sender, routes => routes.MapCommand<LiveCommand>(queue));
        var command = new LiveCommand(Guid.NewGuid(), "scheduled payload");
        var headers = CreateHeaders("scheduled");
        var dueTime = DateTimeOffset.UtcNow.AddSeconds(8);

        await dispatcher.ScheduleAsync(command, dueTime, headers);

        var early = await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(2));
        Assert.Null(early);

        var received = await LiveServiceBusWait.ReceiveAsync(receiver, queue, ReceiveTimeout);
        try
        {
            Assert.True(
                received.EnqueuedTime >= dueTime.Subtract(TimeSpan.FromSeconds(2)),
                $"Message was enqueued at {received.EnqueuedTime:O}, before the accepted tolerance for {dueTime:O}.");
            var deserialized = Assert.IsType<LiveCommand>(
                serializer.Deserialize(received.Body, typeof(LiveCommand)));
            Assert.Equal(command, deserialized);
            AssertMetadata(received, headers);
        }
        finally
        {
            await receiver.CompleteMessageAsync(received);
        }
    }

    private static AzureServiceBusTransportDispatcher CreateDispatcher(
        IMessageSerializer serializer,
        IAzureServiceBusSender sender,
        Action<AzureServiceBusTransportRoutes> configureRoutes)
    {
        var routes = new AzureServiceBusTransportRoutes();
        configureRoutes(routes);
        return new AzureServiceBusTransportDispatcher(
            routes,
            new AzureServiceBusMessageFactory(serializer),
            sender);
    }

    private static Dictionary<string, string> CreateHeaders(string suffix)
    {
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [HeaderNames.MessageId] = $"live-message-{suffix}-{Guid.NewGuid():N}",
            [HeaderNames.CorrelationId] = $"live-correlation-{Guid.NewGuid():N}",
            [HeaderNames.CausationId] = $"live-causation-{Guid.NewGuid():N}",
            [HeaderNames.ContentType] = "application/json",
            ["MiniBus.Live.Custom"] = $"custom-{suffix}"
        };
    }

    private static void AssertMetadata(
        ServiceBusReceivedMessage received,
        IReadOnlyDictionary<string, string> expectedHeaders)
    {
        Assert.Equal(expectedHeaders[HeaderNames.MessageId], received.MessageId);
        Assert.Equal(expectedHeaders[HeaderNames.CorrelationId], received.CorrelationId);
        Assert.Equal(expectedHeaders[HeaderNames.ContentType], received.ContentType);
        Assert.False(string.IsNullOrWhiteSpace(received.Subject));
        Assert.Contains("Live", received.Subject, StringComparison.Ordinal);
        Assert.Equal(
            expectedHeaders[HeaderNames.CausationId],
            received.ApplicationProperties[HeaderNames.CausationId]);
        Assert.Equal(
            expectedHeaders["MiniBus.Live.Custom"],
            received.ApplicationProperties["MiniBus.Live.Custom"]);
        Assert.True(received.ApplicationProperties.ContainsKey(HeaderNames.MessageType));
        Assert.True(received.ApplicationProperties.ContainsKey(HeaderNames.EnclosedMessageTypes));
    }

    public sealed record LiveCommand(Guid Id, string Value) : ICommand;

    public sealed record LiveEvent(Guid Id, string Value) : IEvent;
}
