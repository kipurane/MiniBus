using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using MiniBus.AzureFunctions.DependencyInjection;
using MiniBus.AzureFunctions.Processing;
using MiniBus.AzureFunctions.Settlement;
using MiniBus.AzureServiceBus.Dispatching;
using MiniBus.AzureServiceBus.Recoverability;
using MiniBus.AzureServiceBus.Routing;
using MiniBus.AzureServiceBus.TransportMessageMapping;
using MiniBus.Core.ClaimCheck;
using MiniBus.Core.Context;
using MiniBus.Core.Contracts;
using MiniBus.Core.Handlers;
using MiniBus.Core.Persistence;
using MiniBus.Core.Recoverability;
using MiniBus.Core.Sagas;
using MiniBus.Core.Serialization;
using Xunit;

namespace MiniBus.AzureFunctions.Tests;

public sealed class MiniBusProcessorTests
{
    private static readonly TimeSpan DelayedRetryAssertionTolerance = TimeSpan.FromSeconds(2);

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
            [MiniBusHeaderNames.CausationId] = "causation-1"
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
        Assert.Equal(RecoverabilityDecisionMaker.RetriesExhaustedDeadLetterReason, actions.DeadLetterReason);
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
    public async Task ProcessAsync_WithoutSettlementPropagatesOriginalHandlerException()
    {
        var processor = CreateProcessor(new RecordingSerializer(new TestCommand(Guid.NewGuid())), services =>
        {
            services.AddSingleton<IHandleMessages<TestCommand>, ThrowingCommandHandler>();
        });
        var message = CreateMessage(new Dictionary<string, object>
        {
            [MiniBusHeaderNames.MessageType] = typeof(TestCommand).AssemblyQualifiedName!
        });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => processor.ProcessAsync(message));

        Assert.Equal("handler failed", exception.Message);
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
    public async Task ProcessAsync_ResolvesClaimCheckBeforeDeserialization()
    {
        var recorder = new HandlerRecorder();
        var serializer = new RecordingSerializer(new TestCommand(Guid.NewGuid()));
        var store = new RecordingClaimCheckPayloadStore
        {
            Payload = BinaryData.FromString("{\"id\":\"restored\"}")
        };
        var processor = CreateProcessor(serializer, services =>
        {
            services.AddSingleton(recorder);
            services.AddSingleton<IHandleMessages<TestCommand>, RecordingCommandHandler>();
            services.AddSingleton<IMiniBusClaimCheckPayloadStore>(store);
        });
        var message = CreateMessage(CreateClaimCheckProperties("message-1", "correlation-1"));
        var actions = new RecordingMessageActions();

        await processor.ProcessAsync(message, actions);

        Assert.Equal("{\"id\":\"restored\"}", serializer.DeserializedBody?.ToString());
        Assert.Single(store.Reads);
        Assert.Single(recorder.Invocations);
        Assert.Equal("message-1", recorder.Invocations[0].Context.MessageId);
        Assert.Equal("correlation-1", recorder.Invocations[0].Context.CorrelationId);
        Assert.Equal(bool.TrueString, recorder.Invocations[0].Context.Headers[MiniBusClaimCheckHeaderNames.Enabled]);
        Assert.Same(message, actions.CompletedMessage);
        Assert.Null(actions.DeadLetteredMessage);
    }

    [Fact]
    public async Task ProcessAsync_DeadLettersWhenClaimCheckPayloadIsMissing()
    {
        var recorder = new HandlerRecorder();
        var store = new RecordingClaimCheckPayloadStore
        {
            Exception = new MiniBusClaimCheckPayloadNotFoundException(CreateClaimCheckReference())
        };
        var processor = CreateProcessor(new RecordingSerializer(new TestCommand(Guid.NewGuid())), services =>
        {
            services.AddSingleton(recorder);
            services.AddSingleton<IHandleMessages<TestCommand>, RecordingCommandHandler>();
            services.AddSingleton<IMiniBusClaimCheckPayloadStore>(store);
        });
        var actions = new RecordingMessageActions();

        await processor.ProcessAsync(CreateMessage(CreateClaimCheckProperties()), actions);

        Assert.Empty(recorder.Invocations);
        Assert.NotNull(actions.DeadLetteredMessage);
        Assert.Contains("claim-check payload", actions.DeadLetterDescription, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProcessAsync_DeadLettersWhenClaimCheckMetadataIsInvalid()
    {
        var recorder = new HandlerRecorder();
        var properties = CreateClaimCheckProperties();
        properties.Remove(MiniBusClaimCheckHeaderNames.BlobName);
        var processor = CreateProcessor(new RecordingSerializer(new TestCommand(Guid.NewGuid())), services =>
        {
            services.AddSingleton(recorder);
            services.AddSingleton<IHandleMessages<TestCommand>, RecordingCommandHandler>();
            services.AddSingleton<IMiniBusClaimCheckPayloadStore>(new RecordingClaimCheckPayloadStore());
        });
        var actions = new RecordingMessageActions();

        await processor.ProcessAsync(CreateMessage(properties), actions);

        Assert.Empty(recorder.Invocations);
        Assert.NotNull(actions.DeadLetteredMessage);
        Assert.Contains(MiniBusClaimCheckHeaderNames.BlobName, actions.DeadLetterDescription, StringComparison.Ordinal);
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
    public async Task ProcessAsync_RetriesHandlerImmediatelyAndCompletesWhenRetrySucceeds()
    {
        var recorder = new HandlerRecorder();
        var sender = new RecordingSender();
        var processor = CreateProcessor(
            new RecordingSerializer(new TestCommand(Guid.NewGuid())),
            services =>
            {
                services.AddSingleton(recorder);
                services.AddSingleton<IHandleMessages<TestCommand>, SucceedsOnSecondAttemptHandler>();
                RegisterTransport(services, sender, routes => routes.MapCommand<TestCommand>("billing-queue"));
            },
            options => options.Recoverability.ImmediateRetries = 1);
        var message = CreateMessage(new Dictionary<string, object>
        {
            [MiniBusHeaderNames.MessageType] = typeof(TestCommand).AssemblyQualifiedName!,
            [MiniBusHeaderNames.MessageId] = "message-1",
            [MiniBusHeaderNames.CorrelationId] = "correlation-1"
        });
        var actions = new RecordingMessageActions();

        await processor.ProcessAsync(message, actions);

        Assert.Equal(2, recorder.Invocations.Count);
        Assert.Same(message, actions.CompletedMessage);
        Assert.Null(actions.DeadLetteredMessage);
        Assert.Empty(sender.Schedules);
    }

    [Fact]
    public async Task ProcessAsync_SchedulesDelayedRetryAndCompletesOriginalWhenImmediateRetriesAreExhausted()
    {
        var recorder = new HandlerRecorder();
        var sender = new RecordingSender();
        var delayedRetryDelay = TimeSpan.FromSeconds(10);
        var processor = CreateProcessor(
            new RecordingSerializer(new TestCommand(Guid.NewGuid())),
            services =>
            {
                services.AddSingleton(recorder);
                services.AddSingleton<IHandleMessages<TestCommand>, RecordingThenThrowingCommandHandler>();
                RegisterTransport(services, sender, routes => routes.MapCommand<TestCommand>("billing-queue"));
            },
            options =>
            {
                options.Recoverability.ImmediateRetries = 1;
                options.Recoverability.DelayedRetries.Add(delayedRetryDelay);
            });
        var expectedDueTime = DateTimeOffset.UtcNow.Add(delayedRetryDelay);
        var message = CreateMessage(new Dictionary<string, object>
        {
            [MiniBusHeaderNames.MessageType] = typeof(TestCommand).AssemblyQualifiedName!,
            [MiniBusHeaderNames.MessageId] = "message-1",
            [MiniBusHeaderNames.CorrelationId] = "correlation-1"
        });
        var actions = new RecordingMessageActions();

        await processor.ProcessAsync(message, actions);

        Assert.Equal(2, recorder.Invocations.Count);
        Assert.Same(message, actions.CompletedMessage);
        Assert.Null(actions.DeadLetteredMessage);
        var schedule = Assert.Single(sender.Schedules);
        Assert.Equal("billing-queue", schedule.Destination);
        Assert.InRange(
            schedule.DueTime,
            expectedDueTime.Subtract(DelayedRetryAssertionTolerance),
            expectedDueTime.Add(DelayedRetryAssertionTolerance));
        Assert.Equal("message-1", schedule.Message.ApplicationProperties[MiniBusHeaderNames.MessageId]);
        Assert.Equal("correlation-1", schedule.Message.ApplicationProperties[MiniBusHeaderNames.CorrelationId]);
        Assert.Equal("sdk-message-id", schedule.Message.ApplicationProperties[MiniBusRecoverabilityHeaderNames.OriginalMessageId]);
        Assert.Equal("0", schedule.Message.ApplicationProperties[MiniBusRecoverabilityHeaderNames.ImmediateAttempt]);
        Assert.Equal("1", schedule.Message.ApplicationProperties[MiniBusRecoverabilityHeaderNames.DelayedAttempt]);
        Assert.Equal("handler failed", schedule.Message.ApplicationProperties[MiniBusRecoverabilityHeaderNames.ExceptionMessage]);
    }

    [Fact]
    public async Task ProcessAsync_DelayedRetryPreservesClaimCheckBodyAndMetadata()
    {
        var recorder = new HandlerRecorder();
        var sender = new RecordingSender();
        var delayedRetryDelay = TimeSpan.FromSeconds(10);
        var store = new RecordingClaimCheckPayloadStore
        {
            Payload = BinaryData.FromString("{\"id\":\"restored\"}")
        };
        var processor = CreateProcessor(
            new RecordingSerializer(new TestCommand(Guid.NewGuid())),
            services =>
            {
                services.AddSingleton(recorder);
                services.AddSingleton<IHandleMessages<TestCommand>, RecordingThenThrowingCommandHandler>();
                services.AddSingleton<IMiniBusClaimCheckPayloadStore>(store);
                RegisterTransport(services, sender, routes => routes.MapCommand<TestCommand>("billing-queue"));
            },
            options =>
            {
                options.Recoverability.ImmediateRetries = 0;
                options.Recoverability.DelayedRetries.Add(delayedRetryDelay);
            });
        var message = ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromString("{\"claimCheck\":true}"),
            messageId: "sdk-message-id",
            correlationId: "sdk-correlation-id",
            properties: CreateClaimCheckProperties("message-1", "correlation-1"));
        var actions = new RecordingMessageActions();

        await processor.ProcessAsync(message, actions);

        var schedule = Assert.Single(sender.Schedules);
        Assert.Equal("{\"claimCheck\":true}", schedule.Message.Body.ToString());
        Assert.Equal(bool.TrueString, schedule.Message.ApplicationProperties[MiniBusClaimCheckHeaderNames.Enabled]);
        Assert.Equal("payloads/payload-1.bin", schedule.Message.ApplicationProperties[MiniBusClaimCheckHeaderNames.BlobName]);
        Assert.Equal("1", schedule.Message.ApplicationProperties[MiniBusRecoverabilityHeaderNames.DelayedAttempt]);
        Assert.Same(message, actions.CompletedMessage);
    }

    [Fact]
    public async Task ProcessAsync_DeadLettersWhenAllRetriesAreExhausted()
    {
        var recorder = new HandlerRecorder();
        var processor = CreateProcessor(
            new RecordingSerializer(new TestCommand(Guid.NewGuid())),
            services =>
            {
                services.AddSingleton(recorder);
                services.AddSingleton<IHandleMessages<TestCommand>, RecordingThenThrowingCommandHandler>();
            },
            options => options.Recoverability.ImmediateRetries = 1);
        var message = CreateMessage(new Dictionary<string, object>
        {
            [MiniBusHeaderNames.MessageType] = typeof(TestCommand).AssemblyQualifiedName!,
            [MiniBusHeaderNames.MessageId] = "message-1",
            [MiniBusHeaderNames.CorrelationId] = "correlation-1"
        });
        var actions = new RecordingMessageActions();

        await processor.ProcessAsync(message, actions);

        Assert.Equal(2, recorder.Invocations.Count);
        Assert.Null(actions.CompletedMessage);
        Assert.Same(message, actions.DeadLetteredMessage);
        Assert.Equal(RecoverabilityDecisionMaker.RetriesExhaustedDeadLetterReason, actions.DeadLetterReason);
        Assert.Contains("ExceptionType=System.InvalidOperationException", actions.DeadLetterDescription, StringComparison.Ordinal);
        Assert.Contains("ExceptionMessage=handler failed", actions.DeadLetterDescription, StringComparison.Ordinal);
        Assert.Contains("ImmediateAttempt=1", actions.DeadLetterDescription, StringComparison.Ordinal);
        Assert.Contains("DelayedAttempt=0", actions.DeadLetterDescription, StringComparison.Ordinal);
        Assert.Contains("OriginalMessageId=sdk-message-id", actions.DeadLetterDescription, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProcessAsync_ThrowsActionableMessageWhenDelayedRetrySchedulerIsMissing()
    {
        var recorder = new HandlerRecorder();
        var processor = CreateProcessor(
            new RecordingSerializer(new TestCommand(Guid.NewGuid())),
            services =>
            {
                services.AddSingleton(recorder);
                services.AddSingleton<IHandleMessages<TestCommand>, RecordingThenThrowingCommandHandler>();
            },
            options =>
            {
                options.Recoverability.DelayedRetries.Add(TimeSpan.FromSeconds(10));
            });
        var message = CreateMessage(new Dictionary<string, object>
        {
            [MiniBusHeaderNames.MessageType] = typeof(TestCommand).AssemblyQualifiedName!
        });
        var actions = new RecordingMessageActions();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => processor.ProcessAsync(message, actions));

        Assert.Null(actions.CompletedMessage);
        Assert.Null(actions.DeadLetteredMessage);
        Assert.Contains("AddMiniBusAzureFunctions", exception.Message, StringComparison.Ordinal);
        Assert.Contains("IAzureServiceBusDelayedRetryScheduler", exception.Message, StringComparison.Ordinal);
        Assert.Contains("AzureServiceBusDelayedRetryScheduler", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddMiniBusAzureFunctions_RegistersProcessor()
    {
        var services = new ServiceCollection()
            .AddSingleton<IMessageSerializer>(new RecordingSerializer(new TestCommand(Guid.NewGuid())));

        services.AddMiniBusAzureFunctions(options => options.EndpointName = "Billing");

        var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<MiniBusProcessor>());
        Assert.Equal("Billing", provider.GetRequiredService<MiniBusProcessorOptions>().EndpointName);
        Assert.False(provider.GetRequiredService<MiniBusProcessorOptions>().EnableSagas);
        Assert.Null(provider.GetService<SagaRegistry>());
        Assert.IsType<UnconfiguredSagaPersistence>(provider.GetRequiredService<ISagaPersistence>());
        Assert.Null(provider.GetService<SagaInvoker>());
    }

    [Fact]
    public async Task ProcessAsync_InvokesSagaThroughCoreAbstractions()
    {
        BillingSaga.HandledCount = 0;
        BillingSaga.LastContext = null;
        var persistence = new InMemorySagaPersistence();
        var registry = new SagaRegistry();
        registry.Register<BillingSaga, BillingSagaData>();
        var processor = CreateProcessor(new RecordingSerializer(new StartBillingSaga("billing-1")), services =>
        {
            services.AddSingleton(registry);
            services.AddSingleton<ISagaPersistence>(persistence);
            services.AddSingleton<SagaInvoker>();
        }, options => options.EnableSagas = true);
        var message = CreateMessage(new Dictionary<string, object>
        {
            [MiniBusHeaderNames.MessageType] = typeof(StartBillingSaga).AssemblyQualifiedName!,
            [MiniBusHeaderNames.MessageId] = "message-1",
            [MiniBusHeaderNames.CorrelationId] = "correlation-1"
        });
        var actions = new RecordingMessageActions();

        await processor.ProcessAsync(message, actions);

        var stored = await persistence.LoadAsync<BillingSagaData>("billing-1");
        Assert.NotNull(stored);
        Assert.Equal("started", stored.Data.Step);
        Assert.Equal(1, BillingSaga.HandledCount);
        Assert.NotNull(BillingSaga.LastContext);
        Assert.Equal("Billing", BillingSaga.LastContext.EndpointName);
        Assert.Equal("message-1", BillingSaga.LastContext.MessageId);
        Assert.Equal("correlation-1", BillingSaga.LastContext.CorrelationId);
        Assert.Same(message, actions.CompletedMessage);
        Assert.Null(actions.DeadLetteredMessage);
    }

    [Fact]
    public async Task ProcessAsync_SagaTimeoutRequestDirectlySchedulesThroughTransportWhenOutboxIsDisabled()
    {
        TimeoutRequestSaga.Reset();
        var sender = new RecordingSender();
        var persistence = new InMemorySagaPersistence();
        var registry = new SagaRegistry();
        registry.Register<TimeoutRequestSaga, BillingSagaData>();
        var dueTime = new DateTimeOffset(2026, 5, 15, 12, 0, 0, TimeSpan.Zero);
        var processor = CreateProcessor(new RecordingSerializer(new StartTimeoutSaga("billing-1", dueTime)), services =>
        {
            services.AddSingleton(registry);
            services.AddSingleton<ISagaPersistence>(persistence);
            services.AddSingleton<SagaInvoker>();
            RegisterTransport(services, sender, routes =>
                routes.MapScheduledMessage<BillingTimeout>("billing-timeouts"));
        }, options => options.EnableSagas = true);
        var message = CreateMessage(new Dictionary<string, object>
        {
            [MiniBusHeaderNames.MessageType] = typeof(StartTimeoutSaga).AssemblyQualifiedName!,
            [MiniBusHeaderNames.MessageId] = "message-1",
            [MiniBusHeaderNames.CorrelationId] = "correlation-1"
        });
        var actions = new RecordingMessageActions();

        await processor.ProcessAsync(message, actions);

        var schedule = Assert.Single(sender.Schedules);
        Assert.Equal("billing-timeouts", schedule.Destination);
        Assert.Equal(dueTime, schedule.DueTime);
        Assert.Equal("message-1", schedule.Message.ApplicationProperties[MiniBusHeaderNames.CausationId]);
        Assert.Equal("correlation-1", schedule.Message.ApplicationProperties[MiniBusHeaderNames.CorrelationId]);
        Assert.Equal(typeof(BillingTimeout).AssemblyQualifiedName, schedule.Message.Subject);
        Assert.Same(message, actions.CompletedMessage);
    }

    [Fact]
    public async Task ProcessAsync_SagaTimeoutRequestIsCapturedAsOutboxScheduleWhenOutboxIsEnabled()
    {
        TimeoutRequestSaga.Reset();
        var sender = new RecordingSender();
        var session = new RecordingPersistenceSession();
        var persistence = new InMemorySagaPersistence();
        var registry = new SagaRegistry();
        registry.Register<TimeoutRequestSaga, BillingSagaData>();
        var dueTime = new DateTimeOffset(2026, 5, 15, 12, 0, 0, TimeSpan.Zero);
        var processor = CreateProcessor(new RecordingSerializer(new StartTimeoutSaga("billing-1", dueTime)), services =>
        {
            services.AddSingleton(registry);
            services.AddSingleton<ISagaPersistence>(persistence);
            services.AddSingleton<SagaInvoker>();
            services.AddSingleton<IMiniBusPersistenceSessionFactory>(new RecordingPersistenceSessionFactory(session));
            RegisterTransport(services, sender, routes =>
                routes.MapScheduledMessage<BillingTimeout>("billing-timeouts"));
        }, options => options.EnableSagas = true);
        var message = CreateMessage(new Dictionary<string, object>
        {
            [MiniBusHeaderNames.MessageType] = typeof(StartTimeoutSaga).AssemblyQualifiedName!,
            [MiniBusHeaderNames.MessageId] = "message-1",
            [MiniBusHeaderNames.CorrelationId] = "correlation-1"
        });
        var actions = new RecordingMessageActions();

        await processor.ProcessAsync(message, actions);

        var operation = Assert.Single(session.CommittedOperations);
        Assert.Equal(MiniBusOutboxOperationKind.Schedule, operation.Kind);
        Assert.Equal(typeof(BillingTimeout), operation.MessageType);
        Assert.Equal(new BillingTimeout("billing-1"), operation.Message);
        Assert.Equal(dueTime, operation.DueTime);
        Assert.Equal("message-1", operation.Headers[MiniBusHeaderNames.CausationId]);
        Assert.Equal("correlation-1", operation.Headers[MiniBusHeaderNames.CorrelationId]);
        Assert.Empty(sender.Schedules);
        Assert.Same(message, actions.CompletedMessage);
    }

    [Fact]
    public async Task ProcessAsync_ThrowsWhenSagasAreEnabledWithoutSagaInvoker()
    {
        var processor = CreateProcessor(
            new RecordingSerializer(new StartBillingSaga("billing-1")),
            configureOptions: options => options.EnableSagas = true);
        var message = CreateMessage(new Dictionary<string, object>
        {
            [MiniBusHeaderNames.MessageType] = typeof(StartBillingSaga).AssemblyQualifiedName!
        });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => processor.ProcessAsync(message));

        Assert.Contains("SagaInvoker is not configured", exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(MiniBusProcessorOptions.EnableSagas), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProcessAsync_CompletesDuplicateSqlInboxMessageWithoutInvokingHandlers()
    {
        var recorder = new HandlerRecorder();
        var session = new RecordingPersistenceSession { IsProcessed = true };
        var processor = CreateProcessor(new RecordingSerializer(new TestCommand(Guid.NewGuid())), services =>
        {
            services.AddSingleton(recorder);
            services.AddSingleton<IHandleMessages<TestCommand>, RecordingCommandHandler>();
            services.AddSingleton<IMiniBusPersistenceSessionFactory>(new RecordingPersistenceSessionFactory(session));
        });
        var message = CreateMessage(new Dictionary<string, object>
        {
            [MiniBusHeaderNames.MessageType] = typeof(TestCommand).AssemblyQualifiedName!,
            [MiniBusRecoverabilityHeaderNames.OriginalMessageId] = "original-message"
        });
        var actions = new RecordingMessageActions();

        await processor.ProcessAsync(message, actions);

        Assert.Empty(recorder.Invocations);
        Assert.Same(message, actions.CompletedMessage);
        Assert.Equal("original-message", session.CheckedMessage?.MessageId);
        Assert.Null(session.CommittedMessage);
    }

    [Fact]
    public async Task ProcessAsync_WithoutSettlementSkipsDuplicateSqlInboxMessageWithoutInvokingHandlers()
    {
        var recorder = new HandlerRecorder();
        var session = new RecordingPersistenceSession { IsProcessed = true };
        var processor = CreateProcessor(new RecordingSerializer(new TestCommand(Guid.NewGuid())), services =>
        {
            services.AddSingleton(recorder);
            services.AddSingleton<IHandleMessages<TestCommand>, RecordingCommandHandler>();
            services.AddSingleton<IMiniBusPersistenceSessionFactory>(new RecordingPersistenceSessionFactory(session));
        });
        var message = CreateMessage(new Dictionary<string, object>
        {
            [MiniBusHeaderNames.MessageType] = typeof(TestCommand).AssemblyQualifiedName!,
            [MiniBusRecoverabilityHeaderNames.OriginalMessageId] = "original-message"
        });

        await processor.ProcessAsync(message);

        Assert.Empty(recorder.Invocations);
        Assert.Equal("original-message", session.CheckedMessage?.MessageId);
        Assert.Null(session.CommittedMessage);
    }

    [Fact]
    public async Task ProcessAsync_WithSqlOutboxCapturesOperationsAndCommitsBeforeCompleting()
    {
        var recorder = new HandlerRecorder();
        var sender = new RecordingSender();
        var session = new RecordingPersistenceSession();
        var actions = new RecordingMessageActions();
        session.OnCommit = () => Assert.Null(actions.CompletedMessage);
        var processor = CreateProcessor(new RecordingSerializer(new TestCommand(Guid.NewGuid())), services =>
        {
            services.AddSingleton(recorder);
            services.AddSingleton<IHandleMessages<TestCommand>, SendingCommandHandler>();
            services.AddSingleton<IMiniBusPersistenceSessionFactory>(new RecordingPersistenceSessionFactory(session));
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
            [MiniBusHeaderNames.CorrelationId] = "correlation-1"
        });

        await processor.ProcessAsync(message, actions);

        Assert.Single(recorder.Invocations);
        Assert.Same(message, actions.CompletedMessage);
        Assert.NotNull(session.CommittedMessage);
        Assert.Equal("message-1", session.CommittedMessage.MessageId);
        Assert.Equal(3, session.CommittedOperations.Count);
        Assert.Contains(session.CommittedOperations, operation => operation.Kind == MiniBusOutboxOperationKind.Send);
        Assert.Contains(session.CommittedOperations, operation => operation.Kind == MiniBusOutboxOperationKind.Publish);
        Assert.Contains(session.CommittedOperations, operation => operation.Kind == MiniBusOutboxOperationKind.Schedule);
        Assert.All(session.CommittedOperations, operation =>
        {
            Assert.Equal("message-1", operation.Headers[MiniBusHeaderNames.CausationId]);
            Assert.Equal("correlation-1", operation.Headers[MiniBusHeaderNames.CorrelationId]);
        });
        Assert.Empty(sender.Sends);
        Assert.Empty(sender.Schedules);
    }

    [Fact]
    public async Task ProcessAsync_WithSqlCommitFailureDoesNotCompleteIncomingMessage()
    {
        var session = new RecordingPersistenceSession
        {
            CommitException = new InvalidOperationException("commit failed")
        };
        var processor = CreateProcessor(new RecordingSerializer(new TestCommand(Guid.NewGuid())), services =>
        {
            services.AddSingleton<IHandleMessages<TestCommand>, RecordingCommandHandler>();
            services.AddSingleton(new HandlerRecorder());
            services.AddSingleton<IMiniBusPersistenceSessionFactory>(new RecordingPersistenceSessionFactory(session));
        });
        var message = CreateMessage(new Dictionary<string, object>
        {
            [MiniBusHeaderNames.MessageType] = typeof(TestCommand).AssemblyQualifiedName!
        });
        var actions = new RecordingMessageActions();

        var exception = await Assert.ThrowsAsync<MiniBusPersistenceCommitException>(
            () => processor.ProcessAsync(message, actions));

        Assert.Equal("commit failed", exception.InnerException?.Message);
        Assert.Null(actions.CompletedMessage);
        Assert.Null(actions.DeadLetteredMessage);
    }

    private static MiniBusProcessor CreateProcessor(
        IMessageSerializer serializer,
        Action<IServiceCollection>? configureServices = null,
        Action<MiniBusProcessorOptions>? configureOptions = null)
    {
        var services = new ServiceCollection();
        configureServices?.Invoke(services);
        var options = new MiniBusProcessorOptions { EndpointName = "Billing" };
        configureOptions?.Invoke(options);

        var provider = services.BuildServiceProvider();

        return new MiniBusProcessor(
            serializer,
            new MessageHandlerInvoker(),
            provider,
            options,
            sagaInvoker: provider.GetService<SagaInvoker>());
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
        services.AddSingleton<IAzureServiceBusDelayedRetryScheduler, AzureServiceBusDelayedRetryScheduler>();
    }

    private static ServiceBusReceivedMessage CreateMessage(Dictionary<string, object>? properties = null)
    {
        return ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromString("{}"),
            messageId: "sdk-message-id",
            correlationId: "sdk-correlation-id",
            properties: properties);
    }

    private static Dictionary<string, object> CreateClaimCheckProperties(
        string messageId = "message-1",
        string correlationId = "correlation-1")
    {
        return new Dictionary<string, object>
        {
            [MiniBusHeaderNames.MessageType] = typeof(TestCommand).AssemblyQualifiedName!,
            [MiniBusHeaderNames.MessageId] = messageId,
            [MiniBusHeaderNames.CorrelationId] = correlationId,
            [MiniBusClaimCheckHeaderNames.Enabled] = bool.TrueString,
            [MiniBusClaimCheckHeaderNames.Provider] = MiniBusClaimCheckProviderNames.AzureBlobStorage,
            [MiniBusClaimCheckHeaderNames.ContainerName] = "minibus-payloads",
            [MiniBusClaimCheckHeaderNames.BlobName] = "payloads/payload-1.bin",
            [MiniBusClaimCheckHeaderNames.PayloadId] = "payload-1",
            [MiniBusClaimCheckHeaderNames.PayloadLength] = "128",
            [MiniBusClaimCheckHeaderNames.ContentType] = "application/json",
            [MiniBusClaimCheckHeaderNames.CreatedUtc] = "2026-05-15T12:00:00.0000000+00:00"
        };
    }

    private static MiniBusClaimCheckPayloadReference CreateClaimCheckReference()
    {
        return new MiniBusClaimCheckPayloadReference(
            MiniBusClaimCheckProviderNames.AzureBlobStorage,
            "minibus-payloads",
            "payloads/payload-1.bin",
            "payload-1",
            128,
            "application/json",
            new DateTimeOffset(2026, 5, 15, 12, 0, 0, TimeSpan.Zero),
            null);
    }

    private sealed record TestCommand(Guid Id) : ICommand;

    private sealed record OutgoingCommand(Guid Id) : ICommand;

    private sealed record OutgoingEvent(Guid Id) : IEvent;

    private sealed record OutgoingMessage(Guid Id) : IMessage;

    private sealed record StartBillingSaga(string BillingId) : ICommand;

    private sealed record StartTimeoutSaga(string BillingId, DateTimeOffset DueTime) : ICommand;

    private sealed record BillingTimeout(string BillingId) : ISagaTimeout;

    private sealed class HandlerRecorder
    {
        public List<Invocation> Invocations { get; } = new();
    }

    private sealed record Invocation(TestCommand Message, MiniBusContext Context);

    private sealed class BillingSagaData : ISagaData
    {
        public Guid Id { get; set; }

        public string CorrelationId { get; set; } = string.Empty;

        public bool IsCompleted { get; set; }

        public string? Step { get; set; }
    }

    private sealed class BillingSaga :
        MiniBusSaga<BillingSagaData>,
        IHandleSagaMessages<StartBillingSaga>
    {
        public static int HandledCount { get; set; }

        public static MiniBusContext? LastContext { get; set; }

        public override void ConfigureHowToFindSaga(SagaMapper<BillingSagaData> mapper)
        {
            mapper.StartsWith<StartBillingSaga>(message => message.BillingId);
        }

        public Task Handle(StartBillingSaga message, MiniBusContext context, CancellationToken cancellationToken)
        {
            HandledCount++;
            LastContext = context;
            Data.Step = "started";
            return Task.CompletedTask;
        }
    }

    private sealed class TimeoutRequestSaga :
        MiniBusSaga<BillingSagaData>,
        IHandleSagaMessages<StartTimeoutSaga>,
        IHandleSagaMessages<BillingTimeout>
    {
        public static int TimeoutHandledCount { get; private set; }

        public static void Reset()
        {
            TimeoutHandledCount = 0;
        }

        public override void ConfigureHowToFindSaga(SagaMapper<BillingSagaData> mapper)
        {
            mapper.StartsWith<StartTimeoutSaga>(message => message.BillingId)
                .Correlate<BillingTimeout>(message => message.BillingId);
        }

        public async Task Handle(StartTimeoutSaga message, MiniBusContext context, CancellationToken cancellationToken)
        {
            Data.Step = "timeout-requested";
            await RequestTimeout(
                    new BillingTimeout(message.BillingId),
                    message.DueTime,
                    context,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        public Task Handle(BillingTimeout message, MiniBusContext context, CancellationToken cancellationToken)
        {
            TimeoutHandledCount++;
            Data.Step = "timed-out";
            return Task.CompletedTask;
        }
    }

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

    private sealed class RecordingThenThrowingCommandHandler : IHandleMessages<TestCommand>
    {
        private readonly HandlerRecorder _recorder;

        public RecordingThenThrowingCommandHandler(HandlerRecorder recorder)
        {
            _recorder = recorder;
        }

        public Task Handle(TestCommand message, MiniBusContext context, CancellationToken cancellationToken)
        {
            _recorder.Invocations.Add(new Invocation(message, context));
            return Task.FromException(new InvalidOperationException("handler failed"));
        }
    }

    private sealed class SucceedsOnSecondAttemptHandler : IHandleMessages<TestCommand>
    {
        private readonly HandlerRecorder _recorder;

        public SucceedsOnSecondAttemptHandler(HandlerRecorder recorder)
        {
            _recorder = recorder;
        }

        public Task Handle(TestCommand message, MiniBusContext context, CancellationToken cancellationToken)
        {
            _recorder.Invocations.Add(new Invocation(message, context));

            return _recorder.Invocations.Count == 1
                ? Task.FromException(new InvalidOperationException("handler failed"))
                : Task.CompletedTask;
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

        public BinaryData? DeserializedBody { get; private set; }

        public BinaryData Serialize(object message, Type messageType)
        {
            return BinaryData.FromString($"serialized:{messageType.Name}");
        }

        public object Deserialize(BinaryData body, Type messageType)
        {
            DeserializedType = messageType;
            DeserializedBody = body;
            return _message;
        }
    }

    private sealed class RecordingClaimCheckPayloadStore : IMiniBusClaimCheckPayloadStore
    {
        public BinaryData Payload { get; set; } = BinaryData.FromString("{}");

        public Exception? Exception { get; init; }

        public List<MiniBusClaimCheckPayloadReference> Reads { get; } = new();

        public Task<MiniBusClaimCheckPayloadReference> WriteAsync(
            BinaryData payload,
            MiniBusClaimCheckPayloadWriteOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<BinaryData> ReadAsync(
            MiniBusClaimCheckPayloadReference reference,
            CancellationToken cancellationToken = default)
        {
            Reads.Add(reference);

            if (Exception is not null)
            {
                return Task.FromException<BinaryData>(Exception);
            }

            return Task.FromResult(Payload);
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

    private sealed class RecordingPersistenceSessionFactory : IMiniBusPersistenceSessionFactory
    {
        private readonly RecordingPersistenceSession _session;

        public RecordingPersistenceSessionFactory(RecordingPersistenceSession session)
        {
            _session = session;
        }

        public ValueTask<IMiniBusPersistenceSession> CreateAsync(CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult<IMiniBusPersistenceSession>(_session);
        }
    }

    private sealed class RecordingPersistenceSession : IMiniBusPersistenceSession
    {
        public bool IsProcessed { get; init; }

        public MiniBusInboxMessage? CheckedMessage { get; private set; }

        public MiniBusInboxMessage? CommittedMessage { get; private set; }

        public List<MiniBusOutboxOperation> CommittedOperations { get; } = new();

        public Exception? CommitException { get; init; }

        public Action? OnCommit { get; set; }

        public Task<bool> IsProcessedAsync(
            MiniBusInboxMessage message,
            CancellationToken cancellationToken = default)
        {
            CheckedMessage = message;
            return Task.FromResult(IsProcessed);
        }

        public Task CommitAsync(
            MiniBusInboxMessage message,
            IReadOnlyCollection<MiniBusOutboxOperation> outboxOperations,
            CancellationToken cancellationToken = default)
        {
            OnCommit?.Invoke();

            if (CommitException is not null)
            {
                return Task.FromException(CommitException);
            }

            CommittedMessage = message;
            CommittedOperations.AddRange(outboxOperations);
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
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
