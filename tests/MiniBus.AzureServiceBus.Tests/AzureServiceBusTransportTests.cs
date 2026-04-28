using Azure.Messaging.ServiceBus;
using MiniBus.AzureServiceBus.Dispatching;
using MiniBus.AzureServiceBus.Routing;
using MiniBus.AzureServiceBus.TransportMessageMapping;
using MiniBus.Core.Contracts;
using MiniBus.Core.Serialization;
using Xunit;

namespace MiniBus.AzureServiceBus.Tests;

public sealed class AzureServiceBusTransportTests
{
    [Fact]
    public void Routes_ResolveCommandQueue()
    {
        var routes = new AzureServiceBusTransportRoutes();
        routes.MapCommand<TestCommand>("billing-queue");

        var destination = routes.GetCommandQueue(typeof(TestCommand));

        Assert.Equal("billing-queue", destination);
    }

    [Fact]
    public void Routes_ResolveEventTopic()
    {
        var routes = new AzureServiceBusTransportRoutes();
        routes.MapEvent<TestEvent>("domain-events");

        var destination = routes.GetEventTopic(typeof(TestEvent));

        Assert.Equal("domain-events", destination);
    }

    [Fact]
    public void Routes_ThrowForConflictingCommandRoute()
    {
        var routes = new AzureServiceBusTransportRoutes();
        routes.MapCommand<TestCommand>("billing-queue");

        var exception = Assert.Throws<AzureServiceBusRouteConflictException>(
            () => routes.MapCommand<TestCommand>("inventory-queue"));

        Assert.Equal(typeof(TestCommand), exception.MessageType);
        Assert.Equal("billing-queue", exception.ExistingDestination);
        Assert.Equal("inventory-queue", exception.RequestedDestination);
    }

    [Fact]
    public void Routes_ThrowForMissingCommandRoute()
    {
        var routes = new AzureServiceBusTransportRoutes();

        var exception = Assert.Throws<AzureServiceBusRouteNotFoundException>(
            () => routes.GetCommandQueue(typeof(TestCommand)));

        Assert.Equal(typeof(TestCommand), exception.MessageType);
        Assert.Equal("send", exception.Operation);
    }

    [Fact]
    public void Routes_ResolveScheduledCommandToCommandQueue()
    {
        var routes = new AzureServiceBusTransportRoutes();
        routes.MapCommand<TestCommand>("billing-queue");

        var destination = routes.GetScheduledDestination(typeof(TestCommand));

        Assert.Equal("billing-queue", destination);
    }

    [Fact]
    public void Routes_ResolveScheduledEventToEventTopic()
    {
        var routes = new AzureServiceBusTransportRoutes();
        routes.MapEvent<TestEvent>("domain-events");

        var destination = routes.GetScheduledDestination(typeof(TestEvent));

        Assert.Equal("domain-events", destination);
    }

    [Fact]
    public void Routes_ResolveGenericScheduledMessageToExplicitDestination()
    {
        var routes = new AzureServiceBusTransportRoutes();
        routes.MapScheduledMessage<TestMessage>("audit-queue");

        var destination = routes.GetScheduledDestination(typeof(TestMessage));

        Assert.Equal("audit-queue", destination);
    }

    [Fact]
    public async Task Dispatcher_SendsCommandToConfiguredQueue()
    {
        var sender = new RecordingSender();
        var dispatcher = CreateDispatcher(sender, routes =>
            routes.MapCommand<TestCommand>("billing-queue"));

        await dispatcher.SendAsync(new TestCommand(Guid.NewGuid()));

        var send = Assert.Single(sender.Sends);
        Assert.Equal("billing-queue", send.Destination);
        Assert.Equal(typeof(TestCommand).AssemblyQualifiedName, send.Message.Subject);
    }

    [Fact]
    public async Task Dispatcher_PublishesEventToConfiguredTopic()
    {
        var sender = new RecordingSender();
        var dispatcher = CreateDispatcher(sender, routes =>
            routes.MapEvent<TestEvent>("domain-events"));

        await dispatcher.PublishAsync(new TestEvent(Guid.NewGuid()));

        var send = Assert.Single(sender.Sends);
        Assert.Equal("domain-events", send.Destination);
        Assert.Equal(typeof(TestEvent).AssemblyQualifiedName, send.Message.Subject);
    }

    [Fact]
    public async Task Dispatcher_SchedulesMessageToResolvedDestination()
    {
        var sender = new RecordingSender();
        var dueTime = DateTimeOffset.UtcNow.AddMinutes(10);
        var dispatcher = CreateDispatcher(sender, routes =>
            routes.MapCommand<TestCommand>("billing-queue"));

        var sequenceNumber = await dispatcher.ScheduleAsync(new TestCommand(Guid.NewGuid()), dueTime);

        Assert.Equal(42L, sequenceNumber);
        var schedule = Assert.Single(sender.Schedules);
        Assert.Equal("billing-queue", schedule.Destination);
        Assert.Equal(dueTime, schedule.ScheduledEnqueueTime);
    }

    [Fact]
    public async Task Dispatcher_DoesNotCallSenderWhenRouteIsMissing()
    {
        var sender = new RecordingSender();
        var dispatcher = CreateDispatcher(sender);

        await Assert.ThrowsAsync<AzureServiceBusRouteNotFoundException>(
            () => dispatcher.SendAsync(new TestCommand(Guid.NewGuid())));

        Assert.Empty(sender.Sends);
        Assert.Empty(sender.Schedules);
    }

    [Fact]
    public void MessageFactory_UsesSerializerAndSetsSerializedBody()
    {
        var serializer = new RecordingSerializer();
        var factory = new AzureServiceBusMessageFactory(serializer);
        var command = new TestCommand(Guid.NewGuid());

        var message = factory.CreateMessage(command, typeof(TestCommand));

        Assert.Same(command, serializer.SerializedMessage);
        Assert.Equal(typeof(TestCommand), serializer.SerializedType);
        Assert.Equal("serialized:TestCommand", message.Body.ToString());
    }

    [Fact]
    public void MessageFactory_MapsMiniBusHeadersToApplicationProperties()
    {
        var factory = new AzureServiceBusMessageFactory(new RecordingSerializer());
        var headers = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [MiniBusHeaderNames.MessageId] = "message-1",
            [MiniBusHeaderNames.CorrelationId] = "correlation-1",
            ["Custom"] = "custom-value"
        };

        var message = factory.CreateMessage(new TestCommand(Guid.NewGuid()), typeof(TestCommand), headers);

        Assert.Equal("message-1", message.ApplicationProperties[MiniBusHeaderNames.MessageId]);
        Assert.Equal("correlation-1", message.ApplicationProperties[MiniBusHeaderNames.CorrelationId]);
        Assert.Equal("custom-value", message.ApplicationProperties["Custom"]);
    }

    [Fact]
    public void HeaderMapper_MapsApplicationPropertiesBackToMiniBusHeaders()
    {
        var properties = new Dictionary<string, object>
        {
            [MiniBusHeaderNames.MessageId] = "message-1",
            ["Attempt"] = 3,
            ["Created"] = new DateTimeOffset(2026, 4, 29, 12, 0, 0, TimeSpan.Zero)
        };

        var headers = AzureServiceBusHeaderMapper.ReadHeaders(properties);

        Assert.Equal("message-1", headers[MiniBusHeaderNames.MessageId]);
        Assert.Equal("3", headers["Attempt"]);
        Assert.Equal("04/29/2026 12:00:00 +00:00", headers["Created"]);
    }

    [Fact]
    public void MessageFactory_MirrorsMiniBusMetadataToServiceBusSystemProperties()
    {
        var factory = new AzureServiceBusMessageFactory(new RecordingSerializer());
        var headers = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [MiniBusHeaderNames.MessageId] = "message-1",
            [MiniBusHeaderNames.CorrelationId] = "correlation-1",
            [MiniBusHeaderNames.ContentType] = "application/vnd.test+json",
            [MiniBusHeaderNames.MessageType] = "Contracts.TestCommand"
        };

        var message = factory.CreateMessage(new TestCommand(Guid.NewGuid()), typeof(TestCommand), headers);

        Assert.Equal("message-1", message.MessageId);
        Assert.Equal("correlation-1", message.CorrelationId);
        Assert.Equal("application/vnd.test+json", message.ContentType);
        Assert.Equal("Contracts.TestCommand", message.Subject);
    }

    private static AzureServiceBusTransportDispatcher CreateDispatcher(
        RecordingSender sender,
        Action<AzureServiceBusTransportRoutes>? configureRoutes = null)
    {
        var routes = new AzureServiceBusTransportRoutes();
        configureRoutes?.Invoke(routes);

        return new AzureServiceBusTransportDispatcher(
            routes,
            new AzureServiceBusMessageFactory(new RecordingSerializer()),
            sender);
    }

    private sealed record TestMessage(Guid Id) : IMessage;

    private sealed record TestCommand(Guid Id) : ICommand;

    private sealed record TestEvent(Guid Id) : IEvent;

    private sealed class RecordingSender : IAzureServiceBusSender
    {
        public List<(string Destination, ServiceBusMessage Message)> Sends { get; } = new();

        public List<(string Destination, ServiceBusMessage Message, DateTimeOffset ScheduledEnqueueTime)> Schedules { get; } = new();

        public Task SendAsync(string destination, ServiceBusMessage message, CancellationToken cancellationToken = default)
        {
            Sends.Add((destination, message));
            return Task.CompletedTask;
        }

        public Task<long> ScheduleAsync(
            string destination,
            ServiceBusMessage message,
            DateTimeOffset scheduledEnqueueTime,
            CancellationToken cancellationToken = default)
        {
            Schedules.Add((destination, message, scheduledEnqueueTime));
            return Task.FromResult(42L);
        }
    }

    private sealed class RecordingSerializer : IMessageSerializer
    {
        public object? SerializedMessage { get; private set; }

        public Type? SerializedType { get; private set; }

        public BinaryData Serialize(object message, Type messageType)
        {
            SerializedMessage = message;
            SerializedType = messageType;
            return BinaryData.FromString($"serialized:{messageType.Name}");
        }

        public object Deserialize(BinaryData body, Type messageType)
        {
            throw new NotSupportedException();
        }
    }
}
