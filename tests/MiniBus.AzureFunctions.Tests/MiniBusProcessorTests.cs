using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using MiniBus.AzureFunctions.DependencyInjection;
using MiniBus.AzureFunctions.Processing;
using MiniBus.AzureFunctions.Settlement;
using MiniBus.AzureServiceBus.Dispatching;
using MiniBus.AzureServiceBus.Routing;
using MiniBus.AzureServiceBus.TransportMessageMapping;
using MiniBus.Core.Context;
using MiniBus.Core.Contracts;
using MiniBus.Core.Handlers;
using MiniBus.Core.Serialization;
using Xunit;

namespace MiniBus.AzureFunctions.Tests;

public sealed class MiniBusProcessorTests
{
    [Fact]
    public async Task ProcessAsync_DeserializesInvokesHandlerAndCompletesMessage()
    {
        var recorder = new HandlerRecorder();
        var serializer = new RecordingSerializer(new TestCommand(Guid.NewGuid()));
        var sender = new RecordingSender();
        var processor = CreateProcessor(serializer, services =>
        {
            services.AddSingleton(recorder);
            services.AddSingleton<IHandleMessages<TestCommand>, SendingCommandHandler>();
            RegisterTransport(services, sender, routes =>
            {
                routes.MapCommand<OutgoingCommand>("outgoing-command-queue");
                routes.MapEvent<OutgoingEvent>("outgoing-events");
                routes.MapScheduledMessage<OutgoingMessage>("scheduled-messages");
            });
        });
        var message = CreateMessage(new Dictionary<string, object>
        {
            [MiniBusHeaderNames.MessageType] = typeof(TestCommand).AssemblyQualifiedName!,
            [MiniBusHeaderNames.MessageId] = "message-1",
            [MiniBusHeaderNames.CorrelationId] = "correlation-1",
            ["Custom"] = "custom-value",
            ["MiniBus.CausationId"] = "causation-1"
        });
        var actions = new RecordingMessageActions();

        await processor.ProcessAsync(message, actions);

        Assert.Equal(typeof(TestCommand), serializer.DeserializedType);
        Assert.Single(recorder.Invocations);
        Assert.Equal("Billing", recorder.Invocations[0].Context.EndpointName);
        Assert.Equal("message-1", recorder.Invocations[0].Context.MessageId);
        Assert.Equal("correlation-1", recorder.Invocations[0].Context.CorrelationId);
        Assert.Equal("causation-1", recorder.Invocations[0].Context.CausationId);
        Assert.Equal("custom-value", recorder.Invocations[0].Context.Headers["Custom"]);
        Assert.Equal(2, sender.Sends.Count);
        Assert.Contains(sender.Sends, send =>
            send.Destination == "outgoing-command-queue"
            && (string)send.Message.ApplicationProperties[MiniBusHeaderNames.CausationId] == "message-1"
            && (string)send.Message.ApplicationProperties[MiniBusHeaderNames.CorrelationId] == "correlation-1");
        Assert.Contains(sender.Sends, send => send.Destination == "outgoing-events");
        var schedule = Assert.Single(sender.Schedules);
        Assert.Equal("scheduled-messages", schedule.Destination);
        Assert.Same(message, actions.CompletedMessage);
        Assert.Null(actions.DeadLetteredMessage);
    }

    [Fact]
    public async Task ProcessAsync_WithoutSettlementProcessesMessageAndDoesNotSettle()
    {
        var recorder = new HandlerRecorder();
        var processor = CreateProcessor(new RecordingSerializer(new TestCommand(Guid.NewGuid())), services =>
        {
            services.AddSingleton(recorder);
            services.AddSingleton<IHandleMessages<TestCommand>, RecordingCommandHandler>();
        });
        var message = CreateMessage(new Dictionary<string, object>
        {
            [MiniBusHeaderNames.MessageType] = typeof(TestCommand).AssemblyQualifiedName!
        });

        await processor.ProcessAsync(message);

        Assert.Single(recorder.Invocations);
    }

    [Fact]
    public async Task ProcessAsync_DeadLettersWhenMessageTypeMetadataIsMissing()
    {
        var recorder = new HandlerRecorder();
        var processor = CreateProcessor(new RecordingSerializer(new TestCommand(Guid.NewGuid())), services =>
        {
            services.AddSingleton(recorder);
            services.AddSingleton<IHandleMessages<TestCommand>, RecordingCommandHandler>();
        });
        var actions = new RecordingMessageActions();

        await processor.ProcessAsync(CreateMessage(), actions);

        Assert.Empty(recorder.Invocations);
        Assert.Null(actions.CompletedMessage);
        Assert.NotNull(actions.DeadLetteredMessage);
        Assert.Equal(MiniBusProcessor.DeadLetterReason, actions.DeadLetterReason);
        Assert.Contains("metadata is missing", actions.DeadLetterDescription, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProcessAsync_WithoutSettlementPropagatesMissingMessageTypeMetadata()
    {
        var processor = CreateProcessor(new RecordingSerializer(new TestCommand(Guid.NewGuid())));

        var exception = await Assert.ThrowsAsync<MiniBusMessageTypeResolutionException>(
            () => processor.ProcessAsync(CreateMessage()));

        Assert.Contains("metadata is missing", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProcessAsync_DeadLettersWhenMessageTypeCannotBeResolved()
    {
        var recorder = new HandlerRecorder();
        var processor = CreateProcessor(new RecordingSerializer(new TestCommand(Guid.NewGuid())), services =>
        {
            services.AddSingleton(recorder);
            services.AddSingleton<IHandleMessages<TestCommand>, RecordingCommandHandler>();
        });
        var message = CreateMessage(new Dictionary<string, object>
        {
            [MiniBusHeaderNames.MessageType] = "Missing.Type, Missing.Assembly"
        });
        var actions = new RecordingMessageActions();

        await processor.ProcessAsync(message, actions);

        Assert.Empty(recorder.Invocations);
        Assert.Null(actions.CompletedMessage);
        Assert.NotNull(actions.DeadLetteredMessage);
        Assert.Contains("could not be resolved", actions.DeadLetterDescription, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProcessAsync_DeadLettersWhenDeserializationFails()
    {
        var recorder = new HandlerRecorder();
        var processor = CreateProcessor(new ThrowingSerializer(), services =>
        {
            services.AddSingleton(recorder);
            services.AddSingleton<IHandleMessages<TestCommand>, RecordingCommandHandler>();
        });
        var message = CreateMessage(new Dictionary<string, object>
        {
            [MiniBusHeaderNames.MessageType] = typeof(TestCommand).AssemblyQualifiedName!
        });
        var actions = new RecordingMessageActions();

        await processor.ProcessAsync(message, actions);

        Assert.Empty(recorder.Invocations);
        Assert.Null(actions.CompletedMessage);
        Assert.NotNull(actions.DeadLetteredMessage);
        Assert.Contains("deserialize failed", actions.DeadLetterDescription, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProcessAsync_DeadLettersWhenHandlerFails()
    {
        var processor = CreateProcessor(new RecordingSerializer(new TestCommand(Guid.NewGuid())), services =>
        {
            services.AddSingleton<IHandleMessages<TestCommand>, ThrowingCommandHandler>();
        });
        var message = CreateMessage(new Dictionary<string, object>
        {
            [MiniBusHeaderNames.MessageType] = typeof(TestCommand).AssemblyQualifiedName!
        });
        var actions = new RecordingMessageActions();

        await processor.ProcessAsync(message, actions);

        Assert.Null(actions.CompletedMessage);
        Assert.NotNull(actions.DeadLetteredMessage);
        Assert.Contains("handler failed", actions.DeadLetterDescription, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProcessAsync_DeadLettersWhenOutgoingDispatchFails()
    {
        var processor = CreateProcessor(new RecordingSerializer(new TestCommand(Guid.NewGuid())), services =>
        {
            services.AddSingleton(new HandlerRecorder());
            services.AddSingleton<IHandleMessages<TestCommand>, SendingCommandHandler>();
            RegisterTransport(services, new RecordingSender(), routes => routes.MapEvent<OutgoingEvent>("outgoing-events"));
        });
        var message = CreateMessage(new Dictionary<string, object>
        {
            [MiniBusHeaderNames.MessageType] = typeof(TestCommand).AssemblyQualifiedName!
        });
        var actions = new RecordingMessageActions();

        await processor.ProcessAsync(message, actions);

        Assert.Null(actions.CompletedMessage);
        Assert.NotNull(actions.DeadLetteredMessage);
        Assert.Contains("No Azure Service Bus route", actions.DeadLetterDescription, StringComparison.Ordinal);
    }

    [Fact]
    public void AddMiniBusAzureFunctions_RegistersProcessor()
    {
        var services = new ServiceCollection()
            .AddSingleton<IMessageSerializer>(new RecordingSerializer(new TestCommand(Guid.NewGuid())));

        services.AddMiniBusAzureFunctions(options => options.EndpointName = "Billing");

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<MiniBusProcessor>());
        Assert.Equal("Billing", provider.GetRequiredService<MiniBusProcessorOptions>().EndpointName);
    }

    private static MiniBusProcessor CreateProcessor(
        IMessageSerializer serializer,
        Action<IServiceCollection>? configureServices = null)
    {
        var services = new ServiceCollection();
        configureServices?.Invoke(services);

        return new MiniBusProcessor(
            serializer,
            new MessageHandlerInvoker(),
            services.BuildServiceProvider(),
            new MiniBusProcessorOptions { EndpointName = "Billing" });
    }

    private static void RegisterTransport(
        IServiceCollection services,
        RecordingSender sender,
        Action<AzureServiceBusTransportRoutes> configureRoutes)
    {
        var routes = new AzureServiceBusTransportRoutes();
        configureRoutes(routes);

        services.AddSingleton(routes);
        services.AddSingleton<IAzureServiceBusSender>(sender);
        services.AddSingleton(new AzureServiceBusMessageFactory(new RecordingSerializer(new object())));
        services.AddSingleton<AzureServiceBusTransportDispatcher>();
    }

    private static ServiceBusReceivedMessage CreateMessage(Dictionary<string, object>? properties = null)
    {
        return ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromString("{}"),
            messageId: "sdk-message-id",
            correlationId: "sdk-correlation-id",
            properties: properties);
    }

    private sealed record TestCommand(Guid Id) : ICommand;

    private sealed record OutgoingCommand(Guid Id) : ICommand;

    private sealed record OutgoingEvent(Guid Id) : IEvent;

    private sealed record OutgoingMessage(Guid Id) : IMessage;

    private sealed class HandlerRecorder
    {
        public List<Invocation> Invocations { get; } = new();
    }

    private sealed record Invocation(TestCommand Message, MiniBusContext Context);

    private sealed class RecordingCommandHandler : IHandleMessages<TestCommand>
    {
        private readonly HandlerRecorder _recorder;

        public RecordingCommandHandler(HandlerRecorder recorder)
        {
            _recorder = recorder;
        }

        public Task Handle(TestCommand message, MiniBusContext context, CancellationToken cancellationToken)
        {
            _recorder.Invocations.Add(new Invocation(message, context));
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingCommandHandler : IHandleMessages<TestCommand>
    {
        public Task Handle(TestCommand message, MiniBusContext context, CancellationToken cancellationToken)
        {
            return Task.FromException(new InvalidOperationException("handler failed"));
        }
    }

    private sealed class SendingCommandHandler : IHandleMessages<TestCommand>
    {
        private readonly HandlerRecorder _recorder;

        public SendingCommandHandler(HandlerRecorder recorder)
        {
            _recorder = recorder;
        }

        public async Task Handle(TestCommand message, MiniBusContext context, CancellationToken cancellationToken)
        {
            _recorder.Invocations.Add(new Invocation(message, context));

            await context.Send(new OutgoingCommand(Guid.NewGuid()), cancellationToken);
            await context.Publish(new OutgoingEvent(Guid.NewGuid()), cancellationToken);
            await context.Schedule(new OutgoingMessage(Guid.NewGuid()), DateTimeOffset.UtcNow.AddMinutes(5), cancellationToken);
        }
    }

    private sealed class RecordingSerializer : IMessageSerializer
    {
        private readonly object _message;

        public RecordingSerializer(object message)
        {
            _message = message;
        }

        public Type? DeserializedType { get; private set; }

        public BinaryData Serialize(object message, Type messageType)
        {
            return BinaryData.FromString($"serialized:{messageType.Name}");
        }

        public object Deserialize(BinaryData body, Type messageType)
        {
            DeserializedType = messageType;
            return _message;
        }
    }

    private sealed class ThrowingSerializer : IMessageSerializer
    {
        public BinaryData Serialize(object message, Type messageType)
        {
            throw new NotSupportedException();
        }

        public object Deserialize(BinaryData body, Type messageType)
        {
            throw new InvalidOperationException("deserialize failed");
        }
    }

    private sealed class RecordingMessageActions : IMiniBusMessageActions
    {
        public ServiceBusReceivedMessage? CompletedMessage { get; private set; }

        public ServiceBusReceivedMessage? DeadLetteredMessage { get; private set; }

        public string? DeadLetterReason { get; private set; }

        public string? DeadLetterDescription { get; private set; }

        public Task CompleteMessageAsync(ServiceBusReceivedMessage message, CancellationToken cancellationToken = default)
        {
            CompletedMessage = message;
            return Task.CompletedTask;
        }

        public Task DeadLetterMessageAsync(
            ServiceBusReceivedMessage message,
            string deadLetterReason,
            string? deadLetterErrorDescription = null,
            CancellationToken cancellationToken = default)
        {
            DeadLetteredMessage = message;
            DeadLetterReason = deadLetterReason;
            DeadLetterDescription = deadLetterErrorDescription;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingSender : IAzureServiceBusSender
    {
        public List<(string Destination, ServiceBusMessage Message)> Sends { get; } = new();

        public List<(string Destination, ServiceBusMessage Message, DateTimeOffset DueTime)> Schedules { get; } = new();

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
            return Task.FromResult(1L);
        }
    }
}
