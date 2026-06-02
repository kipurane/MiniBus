using Azure.Messaging.ServiceBus;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MiniBus.AzureFunctions.DependencyInjection;
using MiniBus.AzureFunctions.Processing;
using MiniBus.AzureFunctions.Processing.Pipeline;
using MiniBus.AzureFunctions.Settlement;
using MiniBus.AzureServiceBus.Dispatching;
using MiniBus.AzureServiceBus.Recoverability;
using MiniBus.AzureServiceBus.Routing;
using MiniBus.AzureServiceBus.TransportMessageMapping;
using MiniBus.Core.Auditing;
using MiniBus.Core.ClaimCheck;
using MiniBus.Core.Context;
using MiniBus.Core.Contracts;
using MiniBus.Core.Handlers;
using MiniBus.Core.Persistence;
using MiniBus.Core.Recoverability;
using MiniBus.Core.Sagas;
using MiniBus.Core.Serialization;

namespace MiniBus.AzureFunctions.Tests;

public sealed class MiniBusProcessorTests
{
    private static readonly TimeSpan DelayedRetryAssertionTolerance = TimeSpan.FromSeconds(2);

    [Fact]
    public async Task ProcessAsync_EmitsProcessingActivityWithCoreTags()
    {
        using var activities = new RecordingActivityListener();
        var processor = CreateProcessor(new RecordingSerializer(new TestCommand(Guid.NewGuid())), services =>
        {
            services.AddSingleton(new HandlerRecorder());
            services.AddSingleton<IHandleMessages<TestCommand>, RecordingCommandHandler>();
        });
        var message = CreateMessage(new Dictionary<string, object>
        {
            [MiniBusHeaderNames.MessageType] = typeof(TestCommand).AssemblyQualifiedName!,
            [MiniBusHeaderNames.MessageId] = "message-1",
            [MiniBusHeaderNames.CorrelationId] = "correlation-1",
            [MiniBusHeaderNames.CausationId] = "causation-1"
        });

        await processor.ProcessAsync(message, new RecordingMessageActions());

        var activity = Assert.Single(activities.Activities);
        Assert.Equal(MiniBusProcessingTracer.SourceName, activity.Source.Name);
        Assert.Equal(MiniBusProcessingTracer.ProcessActivityName, activity.OperationName);
        Assert.Equal(ActivityKind.Consumer, activity.Kind);
        Assert.Equal(ActivityStatusCode.Unset, activity.Status);
        Assert.Equal("azure_service_bus", GetTag(activity, MiniBusProcessingTraceTags.MessagingSystem));
        Assert.Equal("Billing", GetTag(activity, MiniBusProcessingTraceTags.MiniBusEndpoint));
        Assert.Equal(typeof(TestCommand).FullName, GetTag(activity, MiniBusProcessingTraceTags.MiniBusMessageType));
        Assert.Equal("message-1", GetTag(activity, MiniBusProcessingTraceTags.MiniBusMessageId));
        Assert.Equal("correlation-1", GetTag(activity, MiniBusProcessingTraceTags.MiniBusCorrelationId));
        Assert.Equal("causation-1", GetTag(activity, MiniBusProcessingTraceTags.MiniBusCausationId));
        Assert.Equal(MiniBusProcessingOutcomes.Completed, GetTag(activity, MiniBusProcessingTraceTags.MiniBusProcessingOutcome));
    }

    [Fact]
    public void StartProcessingActivity_IsNoopWithoutListener()
    {
        var tracer = new MiniBusProcessingTracer();
        var context = new MiniBusProcessingContext(CreateMessage(), new MiniBusProcessorOptions());

        using var activity = tracer.StartProcessingActivity(context);

        Assert.Null(activity);
        Assert.Null(context.ProcessingActivity);
    }

    [Fact]
    public async Task ProcessAsync_CompletesWithoutListener()
    {
        var processor = CreateProcessor(new RecordingSerializer(new TestCommand(Guid.NewGuid())), services =>
        {
            services.AddSingleton(new HandlerRecorder());
            services.AddSingleton<IHandleMessages<TestCommand>, RecordingCommandHandler>();
        });

        await processor.ProcessAsync(CreateMessage(new Dictionary<string, object>
        {
            [MiniBusHeaderNames.MessageType] = typeof(TestCommand).AssemblyQualifiedName!
        }));
    }

    [Fact]
    public async Task ProcessAsync_EmitsSeparateActivitiesForImmediateRetryAttempts()
    {
        using var activities = new RecordingActivityListener();
        var recorder = new HandlerRecorder();
        var processor = CreateProcessor(
            new RecordingSerializer(new TestCommand(Guid.NewGuid())),
            services =>
            {
                services.AddSingleton(recorder);
                services.AddSingleton<IHandleMessages<TestCommand>, SucceedsOnSecondAttemptHandler>();
            },
            options => options.Recoverability.ImmediateRetries = 1);

        await processor.ProcessAsync(
            CreateMessage(new Dictionary<string, object>
            {
                [MiniBusHeaderNames.MessageType] = typeof(TestCommand).AssemblyQualifiedName!,
                [MiniBusHeaderNames.MessageId] = "message-1",
                [MiniBusHeaderNames.CorrelationId] = "correlation-1"
            }),
            new RecordingMessageActions());

        var attempts = activities.Activities.ToArray();
        Assert.Equal(2, attempts.Length);
        Assert.Equal(MiniBusProcessingOutcomes.Retried, GetTag(attempts[0], MiniBusProcessingTraceTags.MiniBusProcessingOutcome));
        Assert.Equal("1", GetTag(attempts[0], MiniBusProcessingTraceTags.MiniBusRetryAttempt));
        Assert.Contains(attempts[0].Events, activityEvent => activityEvent.Name == MiniBusProcessingTraceEvents.Retried);
        Assert.Equal(ActivityStatusCode.Unset, attempts[0].Status);
        Assert.Equal(MiniBusProcessingOutcomes.Completed, GetTag(attempts[1], MiniBusProcessingTraceTags.MiniBusProcessingOutcome));
        Assert.Equal("1", GetTag(attempts[1], MiniBusProcessingTraceTags.MiniBusRetryAttempt));
    }

    [Fact]
    public async Task ProcessAsync_EmitsTracingOutcomesForDelayedDeadLetterDuplicateAndFailure()
    {
        using (var delayedActivities = new RecordingActivityListener())
        {
            var sender = new RecordingSender();
            var delayedProcessor = CreateProcessor(
                new RecordingSerializer(new TestCommand(Guid.NewGuid())),
                services =>
                {
                    services.AddSingleton(new HandlerRecorder());
                    services.AddSingleton<IHandleMessages<TestCommand>, RecordingThenThrowingCommandHandler>();
                    RegisterTransport(services, sender, routes => routes.MapCommand<TestCommand>("billing-queue"));
                },
                options => options.Recoverability.DelayedRetries.Add(TimeSpan.FromSeconds(10)));

            await delayedProcessor.ProcessAsync(
                CreateMessage(new Dictionary<string, object>
                {
                    [MiniBusHeaderNames.MessageType] = typeof(TestCommand).AssemblyQualifiedName!,
                    [MiniBusHeaderNames.MessageId] = "message-1"
                }),
                new RecordingMessageActions());

            var delayed = Assert.Single(delayedActivities.Activities);
            Assert.Equal(MiniBusProcessingOutcomes.DelayedRetryScheduled, GetTag(delayed, MiniBusProcessingTraceTags.MiniBusProcessingOutcome));
            Assert.Equal("1", GetTag(delayed, MiniBusProcessingTraceTags.MiniBusDelayedRetryAttempt));
            Assert.Equal(ActivityStatusCode.Unset, delayed.Status);
            Assert.Contains(delayed.Events, activityEvent => activityEvent.Name == MiniBusProcessingTraceEvents.DelayedRetryScheduled);
        }

        using (var deadLetterActivities = new RecordingActivityListener())
        {
            var deadLetterProcessor = CreateProcessor(new RecordingSerializer(new TestCommand(Guid.NewGuid())), services =>
            {
                services.AddSingleton<IHandleMessages<TestCommand>, ThrowingCommandHandler>();
            });

            await deadLetterProcessor.ProcessAsync(
                CreateMessage(new Dictionary<string, object>
                {
                    [MiniBusHeaderNames.MessageType] = typeof(TestCommand).AssemblyQualifiedName!
                }),
                new RecordingMessageActions());

            var deadLetter = Assert.Single(deadLetterActivities.Activities);
            Assert.Equal(MiniBusProcessingOutcomes.DeadLettered, GetTag(deadLetter, MiniBusProcessingTraceTags.MiniBusProcessingOutcome));
            Assert.Equal(RecoverabilityDecisionMaker.RetriesExhaustedDeadLetterReason, GetTag(deadLetter, MiniBusProcessingTraceTags.MiniBusDeadLetterReason));
            Assert.Equal(ActivityStatusCode.Error, deadLetter.Status);
            Assert.Contains(deadLetter.Events, activityEvent => activityEvent.Name == MiniBusProcessingTraceEvents.DeadLettered);
        }

        using (var duplicateActivities = new RecordingActivityListener())
        {
            var session = new RecordingPersistenceSession { IsProcessed = true };
            var duplicateProcessor = CreateProcessor(new RecordingSerializer(new TestCommand(Guid.NewGuid())), services =>
            {
                services.AddSingleton(new HandlerRecorder());
                services.AddSingleton<IHandleMessages<TestCommand>, RecordingCommandHandler>();
                services.AddSingleton<IMiniBusPersistenceSessionFactory>(new RecordingPersistenceSessionFactory(session));
            });

            await duplicateProcessor.ProcessAsync(
                CreateMessage(new Dictionary<string, object>
                {
                    [MiniBusHeaderNames.MessageType] = typeof(TestCommand).AssemblyQualifiedName!,
                    [MiniBusRecoverabilityHeaderNames.OriginalMessageId] = "original-message"
                }),
                new RecordingMessageActions());

            var duplicate = Assert.Single(duplicateActivities.Activities);
            Assert.Equal(MiniBusProcessingOutcomes.SkippedDuplicate, GetTag(duplicate, MiniBusProcessingTraceTags.MiniBusProcessingOutcome));
            Assert.Equal("original-message", GetTag(duplicate, MiniBusProcessingTraceTags.MiniBusMessageId));
            Assert.Equal(ActivityStatusCode.Unset, duplicate.Status);
            Assert.Contains(duplicate.Events, activityEvent => activityEvent.Name == MiniBusProcessingTraceEvents.SkippedDuplicate);
        }

        using (var failureActivities = new RecordingActivityListener())
        {
            var failureProcessor = CreateProcessor(
                new RecordingSerializer(new TestCommand(Guid.NewGuid())),
                services =>
                {
                    services.AddSingleton<IHandleMessages<TestCommand>, ThrowingCommandHandler>();
                },
                options => options.Recoverability.DeadLetterAfterRetriesExhausted = false);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => failureProcessor.ProcessAsync(CreateMessage(new Dictionary<string, object>
                {
                    [MiniBusHeaderNames.MessageType] = typeof(TestCommand).AssemblyQualifiedName!
                })));

            Assert.Equal("handler failed", exception.Message);
            var failure = Assert.Single(failureActivities.Activities);
            Assert.Equal(MiniBusProcessingOutcomes.Failed, GetTag(failure, MiniBusProcessingTraceTags.MiniBusProcessingOutcome));
            Assert.Equal(typeof(InvalidOperationException).FullName, GetTag(failure, MiniBusProcessingTraceTags.ExceptionType));
            Assert.Equal("handler failed", GetTag(failure, MiniBusProcessingTraceTags.ExceptionMessage));
            Assert.Equal(ActivityStatusCode.Error, failure.Status);
            Assert.Contains(failure.Events, activityEvent => activityEvent.Name == MiniBusProcessingTraceEvents.Failed);
        }
    }

    [Fact]
    public async Task ProcessAsync_EmitsHandlerSagaAndOutboxTraceEvents()
    {
        using var activities = new RecordingActivityListener();
        var persistence = new InMemorySagaPersistence();
        var registry = new SagaRegistry();
        registry.Register<CompletingSaga, BillingSagaData>();
        var session = new RecordingPersistenceSession();
        var sender = new RecordingSender();
        var processor = CreateProcessor(new RecordingSerializer(new CompleteBillingSaga("billing-1")), services =>
        {
            services.AddSingleton(new HandlerRecorder());
            services.AddSingleton(registry);
            services.AddSingleton<ISagaPersistence>(persistence);
            services.AddSingleton<SagaInvoker>();
            services.AddSingleton<IMiniBusPersistenceSessionFactory>(new RecordingPersistenceSessionFactory(session));
            RegisterTransport(services, sender, routes =>
                routes.MapScheduledMessage<BillingTimeout>("billing-timeouts"));
        }, options => options.EnableSagas = true);

        await processor.ProcessAsync(
            CreateMessage(new Dictionary<string, object>
            {
                [MiniBusHeaderNames.MessageType] = typeof(CompleteBillingSaga).AssemblyQualifiedName!,
                [MiniBusHeaderNames.MessageId] = "message-1",
                [MiniBusHeaderNames.CorrelationId] = "correlation-1"
            }),
            new RecordingMessageActions());

        var activity = Assert.Single(activities.Activities);
        Assert.Contains(activity.Events, activityEvent => activityEvent.Name == MiniBusProcessingTraceEvents.SagaInvoked);
        Assert.Contains(activity.Events, activityEvent => activityEvent.Name == MiniBusProcessingTraceEvents.SagaCompleted);
        Assert.Equal(typeof(CompletingSaga).FullName, GetTag(activity, MiniBusProcessingTraceTags.MiniBusSagaType));
        Assert.Equal("billing-1", GetTag(activity, MiniBusProcessingTraceTags.MiniBusSagaCorrelationId));

        using var outboxActivities = new RecordingActivityListener();
        var outboxSession = new RecordingPersistenceSession();
        var outboxProcessor = CreateProcessor(new RecordingSerializer(new TestCommand(Guid.NewGuid())), services =>
        {
            services.AddSingleton(new HandlerRecorder());
            services.AddSingleton<IHandleMessages<TestCommand>, SendingCommandHandler>();
            services.AddSingleton<IMiniBusPersistenceSessionFactory>(new RecordingPersistenceSessionFactory(outboxSession));
            RegisterTransport(services, sender, routes =>
            {
                routes.MapCommand<OutgoingCommand>("outgoing-command-queue");
                routes.MapEvent<OutgoingEvent>("outgoing-events");
                routes.MapScheduledMessage<OutgoingMessage>("scheduled-messages");
            });
        });

        await outboxProcessor.ProcessAsync(
            CreateMessage(new Dictionary<string, object>
            {
                [MiniBusHeaderNames.MessageType] = typeof(TestCommand).AssemblyQualifiedName!,
                [MiniBusHeaderNames.MessageId] = "message-2"
            }),
            new RecordingMessageActions());

        var outboxActivity = Assert.Single(outboxActivities.Activities);
        Assert.Contains(outboxActivity.Events, activityEvent => activityEvent.Name == MiniBusProcessingTraceEvents.HandlerInvoked);
        Assert.Contains(outboxActivity.Events, activityEvent => activityEvent.Name == MiniBusProcessingTraceEvents.OutboxCommitted);
        Assert.Equal(typeof(SendingCommandHandler).FullName, GetTag(outboxActivity, MiniBusProcessingTraceTags.MiniBusHandlerType));
        Assert.Equal(3, GetTagObject(outboxActivity, MiniBusProcessingTraceTags.MiniBusOutboxOperationCount));
    }

    [Fact]
    public async Task ProcessAsync_EmitsErrorActivityForAuditFailure()
    {
        using var activities = new RecordingActivityListener();
        var auditWriter = new RecordingAuditWriter
        {
            Exception = new InvalidOperationException("audit failed")
        };
        var processor = CreateProcessor(new RecordingSerializer(new TestCommand(Guid.NewGuid())), services =>
        {
            services.AddSingleton(auditWriter);
            services.AddSingleton<IMiniBusAuditWriter>(auditWriter);
            services.AddSingleton(new HandlerRecorder());
            services.AddSingleton<IHandleMessages<TestCommand>, RecordingCommandHandler>();
        });

        var exception = await Assert.ThrowsAsync<MiniBusAuditWriteException>(
            () => processor.ProcessAsync(
                CreateMessage(new Dictionary<string, object>
                {
                    [MiniBusHeaderNames.MessageType] = typeof(TestCommand).AssemblyQualifiedName!
                }),
                new RecordingMessageActions()));

        Assert.Equal("audit failed", exception.InnerException?.Message);
        var activity = Assert.Single(activities.Activities);
        Assert.Equal(MiniBusProcessingOutcomes.Failed, GetTag(activity, MiniBusProcessingTraceTags.MiniBusProcessingOutcome));
        Assert.Equal(typeof(MiniBusAuditWriteException).FullName, GetTag(activity, MiniBusProcessingTraceTags.ExceptionType));
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
    }

    [Fact]
    public async Task ProcessAsync_EmitsProcessingMetricsForSuccessfulProcessing()
    {
        using var metrics = new RecordingMeterListener(MiniBusProcessingMetrics.MeterName);
        var processor = CreateProcessor(new RecordingSerializer(new TestCommand(Guid.NewGuid())), services =>
        {
            services.AddSingleton(new HandlerRecorder());
            services.AddSingleton<IHandleMessages<TestCommand>, RecordingCommandHandler>();
        });

        await processor.ProcessAsync(CreateMessage(new Dictionary<string, object>
        {
            [MiniBusHeaderNames.MessageType] = typeof(TestCommand).AssemblyQualifiedName!,
            [MiniBusHeaderNames.MessageId] = "message-1",
            [MiniBusHeaderNames.CorrelationId] = "correlation-1",
            [MiniBusHeaderNames.CausationId] = "causation-1"
        }), new RecordingMessageActions());

        var attempts = Assert.Single(metrics.Measurements, measurement =>
            measurement.InstrumentName == MiniBusProcessingMetrics.ProcessingAttemptsInstrumentName);
        Assert.Equal(1L, attempts.Value);
        Assert.Equal(MiniBusProcessingMetrics.MeterName, attempts.MeterName);
        Assert.Equal(MiniBusProcessingMetrics.AttemptsUnit, attempts.Unit);
        Assert.Equal("Billing", attempts.Tags[MiniBusProcessingMetricTags.MiniBusEndpoint]);
        Assert.Equal(typeof(TestCommand).FullName, attempts.Tags[MiniBusProcessingMetricTags.MiniBusMessageType]);
        Assert.Equal(MiniBusProcessingOutcomes.Completed, attempts.Tags[MiniBusProcessingMetricTags.MiniBusProcessingOutcome]);

        var duration = Assert.Single(metrics.Measurements, measurement =>
            measurement.InstrumentName == MiniBusProcessingMetrics.ProcessingDurationInstrumentName);
        Assert.Equal(MiniBusProcessingMetrics.DurationUnit, duration.Unit);
        Assert.True((double)duration.Value >= 0);

        Assert.DoesNotContain(attempts.Tags.Keys, key => key.Contains("message_id", StringComparison.Ordinal));
        Assert.DoesNotContain(attempts.Tags.Keys, key => key.Contains("correlation_id", StringComparison.Ordinal));
        Assert.DoesNotContain(attempts.Tags.Keys, key => key.Contains("causation_id", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ProcessAsync_EmitsRecoverabilityMetricsForRetryDelayedDeadLetterDuplicateAndFailure()
    {
        using var metrics = new RecordingMeterListener(MiniBusProcessingMetrics.MeterName);
        var retryRecorder = new HandlerRecorder();
        var retryProcessor = CreateProcessor(
            new RecordingSerializer(new TestCommand(Guid.NewGuid())),
            services =>
            {
                services.AddSingleton(retryRecorder);
                services.AddSingleton<IHandleMessages<TestCommand>, SucceedsOnSecondAttemptHandler>();
            },
            options => options.Recoverability.ImmediateRetries = 1);

        await retryProcessor.ProcessAsync(
            CreateMessage(new Dictionary<string, object>
            {
                [MiniBusHeaderNames.MessageType] = typeof(TestCommand).AssemblyQualifiedName!
            }),
            new RecordingMessageActions());

        var immediateRetry = Assert.Single(metrics.Measurements, measurement =>
            measurement.InstrumentName == MiniBusProcessingMetrics.ProcessingRetriesInstrumentName
            && (string?)measurement.Tags[MiniBusProcessingMetricTags.MiniBusRetryKind] == MiniBusProcessingRetryKinds.Immediate);
        Assert.Equal(1L, immediateRetry.Value);

        var sender = new RecordingSender();
        var delayedProcessor = CreateProcessor(
            new RecordingSerializer(new TestCommand(Guid.NewGuid())),
            services =>
            {
                services.AddSingleton(new HandlerRecorder());
                services.AddSingleton<IHandleMessages<TestCommand>, RecordingThenThrowingCommandHandler>();
                RegisterTransport(services, sender, routes => routes.MapCommand<TestCommand>("billing-queue"));
            },
            options => options.Recoverability.DelayedRetries.Add(TimeSpan.FromSeconds(10)));

        await delayedProcessor.ProcessAsync(
            CreateMessage(new Dictionary<string, object>
            {
                [MiniBusHeaderNames.MessageType] = typeof(TestCommand).AssemblyQualifiedName!
            }),
            new RecordingMessageActions());

        var delayedRetry = Assert.Single(metrics.Measurements, measurement =>
            measurement.InstrumentName == MiniBusProcessingMetrics.ProcessingRetriesInstrumentName
            && (string?)measurement.Tags[MiniBusProcessingMetricTags.MiniBusRetryKind] == MiniBusProcessingRetryKinds.Delayed);
        Assert.Equal(1L, delayedRetry.Value);

        var deadLetterProcessor = CreateProcessor(new RecordingSerializer(new TestCommand(Guid.NewGuid())), services =>
        {
            services.AddSingleton<IHandleMessages<TestCommand>, ThrowingCommandHandler>();
        });

        await deadLetterProcessor.ProcessAsync(
            CreateMessage(new Dictionary<string, object>
            {
                [MiniBusHeaderNames.MessageType] = typeof(TestCommand).AssemblyQualifiedName!
            }),
            new RecordingMessageActions());

        Assert.Single(metrics.Measurements, measurement =>
            measurement.InstrumentName == MiniBusProcessingMetrics.ProcessingDeadLettersInstrumentName);

        var duplicateSession = new RecordingPersistenceSession { IsProcessed = true };
        var duplicateProcessor = CreateProcessor(new RecordingSerializer(new TestCommand(Guid.NewGuid())), services =>
        {
            services.AddSingleton(new HandlerRecorder());
            services.AddSingleton<IHandleMessages<TestCommand>, RecordingCommandHandler>();
            services.AddSingleton<IMiniBusPersistenceSessionFactory>(new RecordingPersistenceSessionFactory(duplicateSession));
        });

        await duplicateProcessor.ProcessAsync(
            CreateMessage(new Dictionary<string, object>
            {
                [MiniBusHeaderNames.MessageType] = typeof(TestCommand).AssemblyQualifiedName!
            }),
            new RecordingMessageActions());

        Assert.Single(metrics.Measurements, measurement =>
            measurement.InstrumentName == MiniBusProcessingMetrics.ProcessingDuplicatesInstrumentName);

        var failureProcessor = CreateProcessor(
            new RecordingSerializer(new TestCommand(Guid.NewGuid())),
            services => services.AddSingleton<IHandleMessages<TestCommand>, ThrowingCommandHandler>(),
            options => options.Recoverability.DeadLetterAfterRetriesExhausted = false);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => failureProcessor.ProcessAsync(CreateMessage(new Dictionary<string, object>
            {
                [MiniBusHeaderNames.MessageType] = typeof(TestCommand).AssemblyQualifiedName!
            })));

        Assert.Single(metrics.Measurements, measurement =>
            measurement.InstrumentName == MiniBusProcessingMetrics.ProcessingFailuresInstrumentName);
    }

    [Fact]
    public async Task ProcessAsync_EmitsHandlerMetricsForEachHandler()
    {
        using var metrics = new RecordingMeterListener(MiniBusProcessingMetrics.MeterName);
        var processor = CreateProcessor(new RecordingSerializer(new TestCommand(Guid.NewGuid())), services =>
        {
            services.AddSingleton(new HandlerRecorder());
            services.AddSingleton<IHandleMessages<TestCommand>, RecordingCommandHandler>();
            services.AddSingleton<IHandleMessages<TestCommand>, SecondRecordingCommandHandler>();
        });

        await processor.ProcessAsync(CreateMessage(new Dictionary<string, object>
        {
            [MiniBusHeaderNames.MessageType] = typeof(TestCommand).AssemblyQualifiedName!
        }));

        var handlerDurations = metrics.Measurements
            .Where(measurement => measurement.InstrumentName == MiniBusProcessingMetrics.HandlerDurationInstrumentName)
            .ToArray();
        Assert.Equal(2, handlerDurations.Length);
        Assert.Contains(handlerDurations, measurement =>
            (string?)measurement.Tags[MiniBusProcessingMetricTags.MiniBusHandlerType] == typeof(RecordingCommandHandler).FullName);
        Assert.Contains(handlerDurations, measurement =>
            (string?)measurement.Tags[MiniBusProcessingMetricTags.MiniBusHandlerType] == typeof(SecondRecordingCommandHandler).FullName);
        Assert.All(handlerDurations, measurement =>
        {
            Assert.Equal(MiniBusProcessingMetricOutcomes.Completed, measurement.Tags[MiniBusProcessingMetricTags.MiniBusHandlerOutcome]);
            Assert.True((double)measurement.Value >= 0);
        });
    }

    [Fact]
    public async Task ProcessAsync_EmitsSagaMetrics()
    {
        using var metrics = new RecordingMeterListener(MiniBusProcessingMetrics.MeterName);
        var persistence = new InMemorySagaPersistence();
        var registry = new SagaRegistry();
        registry.Register<CompletingSaga, BillingSagaData>();
        var processor = CreateProcessor(new RecordingSerializer(new CompleteBillingSaga("billing-1")), services =>
        {
            services.AddSingleton(registry);
            services.AddSingleton<ISagaPersistence>(persistence);
            services.AddSingleton<SagaInvoker>();
        }, options => options.EnableSagas = true);

        await processor.ProcessAsync(CreateMessage(new Dictionary<string, object>
        {
            [MiniBusHeaderNames.MessageType] = typeof(CompleteBillingSaga).AssemblyQualifiedName!,
            [MiniBusHeaderNames.CorrelationId] = "correlation-1"
        }));

        var sagaDuration = Assert.Single(metrics.Measurements, measurement =>
            measurement.InstrumentName == MiniBusProcessingMetrics.SagaDurationInstrumentName);
        Assert.Equal(typeof(CompletingSaga).FullName, sagaDuration.Tags[MiniBusProcessingMetricTags.MiniBusSagaType]);
        Assert.Equal(MiniBusProcessingMetricOutcomes.Completed, sagaDuration.Tags[MiniBusProcessingMetricTags.MiniBusSagaOutcome]);
        Assert.False(sagaDuration.Tags.ContainsKey("minibus.saga_correlation_id"));
        Assert.False(sagaDuration.Tags.ContainsKey("minibus.correlation_id"));

        var sagaCompletion = Assert.Single(metrics.Measurements, measurement =>
            measurement.InstrumentName == MiniBusProcessingMetrics.SagaCompletionsInstrumentName);
        Assert.Equal(1L, sagaCompletion.Value);
    }

    [Fact]
    public async Task ProcessAsync_EmitsStructuredStartScopeAndCompletionLogs()
    {
        var logs = new RecordingLoggerFactory();
        var recorder = new HandlerRecorder();
        var processor = CreateProcessor(new RecordingSerializer(new TestCommand(Guid.NewGuid())), services =>
        {
            services.AddSingleton<ILoggerFactory>(logs);
            services.AddSingleton(recorder);
            services.AddSingleton<IHandleMessages<TestCommand>, RecordingCommandHandler>();
        });
        var message = CreateMessage(new Dictionary<string, object>
        {
            [MiniBusHeaderNames.MessageType] = typeof(TestCommand).AssemblyQualifiedName!,
            [MiniBusHeaderNames.MessageId] = "message-1",
            [MiniBusHeaderNames.CorrelationId] = "correlation-1",
            [MiniBusHeaderNames.CausationId] = "causation-1"
        });

        await processor.ProcessAsync(message, new RecordingMessageActions());

        var started = Assert.Single(logs.Entries, entry => entry.EventId == MiniBusProcessingLogEvents.ProcessingStarted);
        Assert.Equal(LogLevel.Information, started.Level);
        Assert.Equal("MiniBus processing started", started.Message);
        Assert.Equal("Billing", started.State[MiniBusProcessingLogProperties.EndpointName]);
        Assert.Equal("message-1", started.State[MiniBusProcessingLogProperties.MessageId]);
        Assert.Equal("correlation-1", started.State[MiniBusProcessingLogProperties.CorrelationId]);
        Assert.Equal("causation-1", started.State[MiniBusProcessingLogProperties.CausationId]);
        Assert.Equal(MiniBusProcessingOutcomes.Started, started.State[MiniBusProcessingLogProperties.ProcessingOutcome]);
        var scope = Assert.Single(started.Scopes);
        Assert.Equal("Billing", scope[MiniBusProcessingLogProperties.EndpointName]);
        Assert.Equal("message-1", scope[MiniBusProcessingLogProperties.MessageId]);
        Assert.Equal("correlation-1", scope[MiniBusProcessingLogProperties.CorrelationId]);
        Assert.Equal("causation-1", scope[MiniBusProcessingLogProperties.CausationId]);

        var completed = Assert.Single(logs.Entries, entry => entry.EventId == MiniBusProcessingLogEvents.ProcessingCompleted);
        Assert.Equal(LogLevel.Information, completed.Level);
        Assert.Equal("MiniBus processing completed", completed.Message);
        Assert.Equal(MiniBusProcessingOutcomes.Completed, completed.State[MiniBusProcessingLogProperties.ProcessingOutcome]);
        Assert.Equal(typeof(TestCommand).FullName, completed.State[MiniBusProcessingLogProperties.MessageType]);
        Assert.Single(completed.Scopes);
    }

    [Fact]
    public async Task ProcessAsync_EmitsStartScopeWithoutCorrelationMetadata()
    {
        var logs = new RecordingLoggerFactory();
        var processor = CreateProcessor(new RecordingSerializer(new TestCommand(Guid.NewGuid())), services =>
        {
            services.AddSingleton<ILoggerFactory>(logs);
            services.AddSingleton(new HandlerRecorder());
            services.AddSingleton<IHandleMessages<TestCommand>, RecordingCommandHandler>();
        });
        var message = ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromString("{}"),
            messageId: "sdk-message-id",
            properties: new Dictionary<string, object>
            {
                [MiniBusHeaderNames.MessageType] = typeof(TestCommand).AssemblyQualifiedName!
            });

        await processor.ProcessAsync(message, new RecordingMessageActions());

        var started = Assert.Single(logs.Entries, entry => entry.EventId == MiniBusProcessingLogEvents.ProcessingStarted);
        var scope = Assert.Single(started.Scopes);
        Assert.Equal("Billing", scope[MiniBusProcessingLogProperties.EndpointName]);
        Assert.Equal("sdk-message-id", scope[MiniBusProcessingLogProperties.MessageId]);
        Assert.False(scope.ContainsKey(MiniBusProcessingLogProperties.CorrelationId));
        Assert.False(scope.ContainsKey(MiniBusProcessingLogProperties.CausationId));
    }

    [Fact]
    public async Task ProcessAsync_EmitsHandlerInvocationDiagnostics()
    {
        var logs = new RecordingLoggerFactory();
        var processor = CreateProcessor(new RecordingSerializer(new TestCommand(Guid.NewGuid())), services =>
        {
            services.AddSingleton<ILoggerFactory>(logs);
            services.AddSingleton(new HandlerRecorder());
            services.AddSingleton<IHandleMessages<TestCommand>, RecordingCommandHandler>();
        });
        var message = CreateMessage(new Dictionary<string, object>
        {
            [MiniBusHeaderNames.MessageType] = typeof(TestCommand).AssemblyQualifiedName!,
            [MiniBusHeaderNames.MessageId] = "message-1",
            [MiniBusHeaderNames.CorrelationId] = "correlation-1"
        });

        await processor.ProcessAsync(message, new RecordingMessageActions());

        var handler = Assert.Single(logs.Entries, entry => entry.EventId == MiniBusProcessingLogEvents.HandlerInvoked);
        Assert.Equal(LogLevel.Information, handler.Level);
        Assert.Equal(MiniBusProcessingOutcomes.HandlerInvoked, handler.State[MiniBusProcessingLogProperties.ProcessingOutcome]);
        Assert.Equal(typeof(RecordingCommandHandler).FullName, handler.State[MiniBusProcessingLogProperties.HandlerType]);
        Assert.Equal(typeof(TestCommand).FullName, handler.State[MiniBusProcessingLogProperties.MessageType]);
        Assert.Equal("correlation-1", handler.State[MiniBusProcessingLogProperties.CorrelationId]);
    }

    [Fact]
    public async Task ProcessAsync_EmitsRetryOutcomeAndNewAttemptScope()
    {
        var logs = new RecordingLoggerFactory();
        var recorder = new HandlerRecorder();
        var processor = CreateProcessor(
            new RecordingSerializer(new TestCommand(Guid.NewGuid())),
            services =>
            {
                services.AddSingleton<ILoggerFactory>(logs);
                services.AddSingleton(recorder);
                services.AddSingleton<IHandleMessages<TestCommand>, SucceedsOnSecondAttemptHandler>();
            },
            options => options.Recoverability.ImmediateRetries = 1);
        var message = CreateMessage(new Dictionary<string, object>
        {
            [MiniBusHeaderNames.MessageType] = typeof(TestCommand).AssemblyQualifiedName!,
            [MiniBusHeaderNames.MessageId] = "message-1",
            [MiniBusHeaderNames.CorrelationId] = "correlation-1"
        });

        await processor.ProcessAsync(message, new RecordingMessageActions());

        var retried = Assert.Single(logs.Entries, entry => entry.EventId == MiniBusProcessingLogEvents.ProcessingRetried);
        Assert.Equal(LogLevel.Warning, retried.Level);
        Assert.Equal(MiniBusProcessingOutcomes.Retried, retried.State[MiniBusProcessingLogProperties.ProcessingOutcome]);
        Assert.Equal(typeof(InvalidOperationException).FullName, retried.State[MiniBusProcessingLogProperties.ExceptionType]);
        Assert.Equal("1", retried.State[MiniBusProcessingLogProperties.RetryAttempt]);
        Assert.Equal("correlation-1", retried.State[MiniBusProcessingLogProperties.CorrelationId]);

        var starts = logs.Entries.Where(entry => entry.EventId == MiniBusProcessingLogEvents.ProcessingStarted).ToArray();
        Assert.Equal(2, starts.Length);
        Assert.Equal("1", starts[1].State[MiniBusProcessingLogProperties.RetryAttempt]);
        Assert.Equal("1", Assert.Single(starts[1].Scopes)[MiniBusProcessingLogProperties.RetryAttempt]);
    }

    [Fact]
    public async Task ProcessAsync_EmitsDelayedRetryDeadLetterDuplicateAndFailureOutcomes()
    {
        var delayedLogs = new RecordingLoggerFactory();
        var delayedSender = new RecordingSender();
        var delayedProcessor = CreateProcessor(
            new RecordingSerializer(new TestCommand(Guid.NewGuid())),
            services =>
            {
                services.AddSingleton<ILoggerFactory>(delayedLogs);
                services.AddSingleton(new HandlerRecorder());
                services.AddSingleton<IHandleMessages<TestCommand>, RecordingThenThrowingCommandHandler>();
                RegisterTransport(services, delayedSender, routes => routes.MapCommand<TestCommand>("billing-queue"));
            },
            options => options.Recoverability.DelayedRetries.Add(TimeSpan.FromSeconds(10)));

        await delayedProcessor.ProcessAsync(
            CreateMessage(new Dictionary<string, object>
            {
                [MiniBusHeaderNames.MessageType] = typeof(TestCommand).AssemblyQualifiedName!,
                [MiniBusHeaderNames.MessageId] = "message-1"
            }),
            new RecordingMessageActions());

        var delayed = Assert.Single(delayedLogs.Entries, entry => entry.EventId == MiniBusProcessingLogEvents.ProcessingDelayedRetryScheduled);
        Assert.Equal(LogLevel.Warning, delayed.Level);
        Assert.Equal(MiniBusProcessingOutcomes.DelayedRetryScheduled, delayed.State[MiniBusProcessingLogProperties.ProcessingOutcome]);
        Assert.Equal("1", delayed.State[MiniBusProcessingLogProperties.DelayedRetryAttempt]);

        var deadLetterLogs = new RecordingLoggerFactory();
        var deadLetterProcessor = CreateProcessor(new RecordingSerializer(new TestCommand(Guid.NewGuid())), services =>
        {
            services.AddSingleton<ILoggerFactory>(deadLetterLogs);
            services.AddSingleton<IHandleMessages<TestCommand>, ThrowingCommandHandler>();
        });

        await deadLetterProcessor.ProcessAsync(
            CreateMessage(new Dictionary<string, object>
            {
                [MiniBusHeaderNames.MessageType] = typeof(TestCommand).AssemblyQualifiedName!
            }),
            new RecordingMessageActions());

        var deadLetter = Assert.Single(deadLetterLogs.Entries, entry => entry.EventId == MiniBusProcessingLogEvents.ProcessingDeadLettered);
        Assert.Equal(LogLevel.Warning, deadLetter.Level);
        Assert.Equal(MiniBusProcessingOutcomes.DeadLettered, deadLetter.State[MiniBusProcessingLogProperties.ProcessingOutcome]);
        Assert.Equal(RecoverabilityDecisionMaker.RetriesExhaustedDeadLetterReason, deadLetter.State[MiniBusProcessingLogProperties.DeadLetterReason]);

        var duplicateLogs = new RecordingLoggerFactory();
        var session = new RecordingPersistenceSession { IsProcessed = true };
        var duplicateProcessor = CreateProcessor(new RecordingSerializer(new TestCommand(Guid.NewGuid())), services =>
        {
            services.AddSingleton<ILoggerFactory>(duplicateLogs);
            services.AddSingleton(new HandlerRecorder());
            services.AddSingleton<IHandleMessages<TestCommand>, RecordingCommandHandler>();
            services.AddSingleton<IMiniBusPersistenceSessionFactory>(new RecordingPersistenceSessionFactory(session));
        });

        await duplicateProcessor.ProcessAsync(
            CreateMessage(new Dictionary<string, object>
            {
                [MiniBusHeaderNames.MessageType] = typeof(TestCommand).AssemblyQualifiedName!,
                [MiniBusRecoverabilityHeaderNames.OriginalMessageId] = "original-message"
            }),
            new RecordingMessageActions());

        var duplicate = Assert.Single(duplicateLogs.Entries, entry => entry.EventId == MiniBusProcessingLogEvents.ProcessingSkippedDuplicate);
        Assert.Equal(LogLevel.Warning, duplicate.Level);
        Assert.Equal(MiniBusProcessingOutcomes.SkippedDuplicate, duplicate.State[MiniBusProcessingLogProperties.ProcessingOutcome]);
        Assert.Equal("original-message", duplicate.State[MiniBusProcessingLogProperties.LogicalMessageId]);

        var failureLogs = new RecordingLoggerFactory();
        var failureProcessor = CreateProcessor(new RecordingSerializer(new TestCommand(Guid.NewGuid())), services =>
        {
            services.AddSingleton<ILoggerFactory>(failureLogs);
            services.AddSingleton<IHandleMessages<TestCommand>, ThrowingCommandHandler>();
        });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => failureProcessor.ProcessAsync(
                CreateMessage(new Dictionary<string, object>
                {
                    [MiniBusHeaderNames.MessageType] = typeof(TestCommand).AssemblyQualifiedName!
                })));

        Assert.Equal("handler failed", exception.Message);
        var failure = Assert.Single(failureLogs.Entries, entry => entry.EventId == MiniBusProcessingLogEvents.ProcessingFailed);
        Assert.Equal(LogLevel.Error, failure.Level);
        Assert.Equal(MiniBusProcessingOutcomes.Failed, failure.State[MiniBusProcessingLogProperties.ProcessingOutcome]);
        Assert.Equal(typeof(InvalidOperationException).FullName, failure.State[MiniBusProcessingLogProperties.ExceptionType]);
    }

    [Fact]
    public async Task ProcessAsync_LogsPropagatedSettlementFailureOnlyOnce()
    {
        var logs = new RecordingLoggerFactory();
        var processor = CreateProcessor(
            new RecordingSerializer(new TestCommand(Guid.NewGuid())),
            services =>
            {
                services.AddSingleton<ILoggerFactory>(logs);
                services.AddSingleton<IHandleMessages<TestCommand>, ThrowingCommandHandler>();
            },
            options => options.Recoverability.DeadLetterAfterRetriesExhausted = false);
        var message = CreateMessage(new Dictionary<string, object>
        {
            [MiniBusHeaderNames.MessageType] = typeof(TestCommand).AssemblyQualifiedName!,
            [MiniBusHeaderNames.MessageId] = "message-1",
            [MiniBusHeaderNames.CorrelationId] = "correlation-1"
        });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => processor.ProcessAsync(message, new RecordingMessageActions()));

        Assert.Equal("handler failed", exception.Message);
        var failure = Assert.Single(logs.Entries, entry => entry.EventId == MiniBusProcessingLogEvents.ProcessingFailed);
        Assert.Equal(LogLevel.Error, failure.Level);
        Assert.Equal(MiniBusProcessingOutcomes.Failed, failure.State[MiniBusProcessingLogProperties.ProcessingOutcome]);
        Assert.Equal(typeof(InvalidOperationException).FullName, failure.State[MiniBusProcessingLogProperties.ExceptionType]);
    }

    [Fact]
    public async Task ProcessAsync_EmitsSagaAndOutboxDiagnostics()
    {
        var sagaLogs = new RecordingLoggerFactory();
        var persistence = new InMemorySagaPersistence();
        var registry = new SagaRegistry();
        registry.Register<CompletingSaga, BillingSagaData>();
        var sagaProcessor = CreateProcessor(new RecordingSerializer(new CompleteBillingSaga("billing-1")), services =>
        {
            services.AddSingleton<ILoggerFactory>(sagaLogs);
            services.AddSingleton(registry);
            services.AddSingleton<ISagaPersistence>(persistence);
            services.AddSingleton<SagaInvoker>();
        }, options => options.EnableSagas = true);

        await sagaProcessor.ProcessAsync(
            CreateMessage(new Dictionary<string, object>
            {
                [MiniBusHeaderNames.MessageType] = typeof(CompleteBillingSaga).AssemblyQualifiedName!,
                [MiniBusHeaderNames.MessageId] = "message-1",
                [MiniBusHeaderNames.CorrelationId] = "correlation-1"
            }),
            new RecordingMessageActions());

        var sagaInvoked = Assert.Single(sagaLogs.Entries, entry => entry.EventId == MiniBusProcessingLogEvents.SagaInvoked);
        Assert.Equal(typeof(CompletingSaga).FullName, sagaInvoked.State[MiniBusProcessingLogProperties.SagaType]);
        Assert.Equal("billing-1", sagaInvoked.State[MiniBusProcessingLogProperties.SagaCorrelationId]);
        var sagaCompleted = Assert.Single(sagaLogs.Entries, entry => entry.EventId == MiniBusProcessingLogEvents.SagaCompleted);
        Assert.Equal(MiniBusProcessingOutcomes.SagaCompleted, sagaCompleted.State[MiniBusProcessingLogProperties.ProcessingOutcome]);

        var outboxLogs = new RecordingLoggerFactory();
        var session = new RecordingPersistenceSession();
        var sender = new RecordingSender();
        var outboxProcessor = CreateProcessor(new RecordingSerializer(new TestCommand(Guid.NewGuid())), services =>
        {
            services.AddSingleton<ILoggerFactory>(outboxLogs);
            services.AddSingleton(new HandlerRecorder());
            services.AddSingleton<IHandleMessages<TestCommand>, SendingCommandHandler>();
            services.AddSingleton<IMiniBusPersistenceSessionFactory>(new RecordingPersistenceSessionFactory(session));
            RegisterTransport(services, sender, routes =>
            {
                routes.MapCommand<OutgoingCommand>("outgoing-command-queue");
                routes.MapEvent<OutgoingEvent>("outgoing-events");
                routes.MapScheduledMessage<OutgoingMessage>("scheduled-messages");
            });
        });

        await outboxProcessor.ProcessAsync(
            CreateMessage(new Dictionary<string, object>
            {
                [MiniBusHeaderNames.MessageType] = typeof(TestCommand).AssemblyQualifiedName!,
                [MiniBusHeaderNames.MessageId] = "message-1"
            }),
            new RecordingMessageActions());

        var outbox = Assert.Single(outboxLogs.Entries, entry => entry.EventId == MiniBusProcessingLogEvents.OutboxCommitted);
        Assert.Equal(LogLevel.Information, outbox.Level);
        Assert.Equal(MiniBusProcessingOutcomes.OutboxCommitted, outbox.State[MiniBusProcessingLogProperties.ProcessingOutcome]);
        Assert.Equal(3, outbox.State[MiniBusProcessingLogProperties.OutboxOperationCount]);
        Assert.DoesNotContain(outboxLogs.Entries, entry =>
            entry.EventId == MiniBusProcessingLogEvents.OutboxCommitted
            && !entry.State.ContainsKey(MiniBusProcessingLogProperties.OutboxOperationCount));
    }


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
            properties: CreateClaimCheckProperties());
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
    public async Task ProcessAsync_WithPersistenceSessionUsesSessionSagaPersistence()
    {
        BillingSaga.HandledCount = 0;
        var session = new RecordingPersistenceSession();
        var fallbackPersistence = new InMemorySagaPersistence();
        var registry = new SagaRegistry();
        registry.Register<BillingSaga, BillingSagaData>();
        var processor = CreateProcessor(new RecordingSerializer(new StartBillingSaga("billing-1")), services =>
        {
            services.AddSingleton(registry);
            services.AddSingleton<ISagaPersistence>(fallbackPersistence);
            services.AddSingleton<SagaInvoker>();
            services.AddSingleton<IMiniBusPersistenceSessionFactory>(new RecordingPersistenceSessionFactory(session));
        }, options => options.EnableSagas = true);
        var message = CreateMessage(new Dictionary<string, object>
        {
            [MiniBusHeaderNames.MessageType] = typeof(StartBillingSaga).AssemblyQualifiedName!,
            [MiniBusHeaderNames.MessageId] = "message-1"
        });

        await processor.ProcessAsync(message, new RecordingMessageActions());

        var sessionStored = await session.LoadAsync<BillingSagaData>("billing-1");
        var fallbackStored = await fallbackPersistence.LoadAsync<BillingSagaData>("billing-1");
        Assert.NotNull(sessionStored);
        Assert.Null(fallbackStored);
        Assert.Equal(2, session.LoadCount);
        Assert.Equal(1, session.CreateCount);
        Assert.Equal(1, BillingSaga.HandledCount);
        Assert.NotNull(session.CommittedMessage);
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
    public async Task ProcessAsync_DuplicateSqlInboxSagaMessageDoesNotInvokeOrMutateSaga()
    {
        BillingSaga.HandledCount = 0;
        var session = new RecordingPersistenceSession { IsProcessed = true };
        var registry = new SagaRegistry();
        registry.Register<BillingSaga, BillingSagaData>();
        var processor = CreateProcessor(new RecordingSerializer(new StartBillingSaga("billing-1")), services =>
        {
            services.AddSingleton(registry);
            services.AddSingleton<ISagaPersistence>(new InMemorySagaPersistence());
            services.AddSingleton<SagaInvoker>();
            services.AddSingleton<IMiniBusPersistenceSessionFactory>(new RecordingPersistenceSessionFactory(session));
        }, options => options.EnableSagas = true);
        var message = CreateMessage(new Dictionary<string, object>
        {
            [MiniBusHeaderNames.MessageType] = typeof(StartBillingSaga).AssemblyQualifiedName!,
            [MiniBusRecoverabilityHeaderNames.OriginalMessageId] = "original-message"
        });
        var actions = new RecordingMessageActions();

        await processor.ProcessAsync(message, actions);

        Assert.Equal(0, BillingSaga.HandledCount);
        Assert.Equal(0, session.LoadCount);
        Assert.Equal(0, session.CreateCount);
        Assert.Equal(0, session.SaveCount);
        Assert.Equal(0, session.CompleteCount);
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

    [Fact]
    public async Task ProcessAsync_WithAuditWriterAuditsSuccessBeforeCompletion()
    {
        var recorder = new HandlerRecorder();
        var auditWriter = new RecordingAuditWriter();
        var actions = new RecordingMessageActions();
        auditWriter.OnWrite = () => Assert.Null(actions.CompletedMessage);
        var auditedUtc = new DateTimeOffset(2026, 5, 15, 12, 0, 0, TimeSpan.Zero);
        var processor = CreateProcessor(
            new RecordingSerializer(new TestCommand(Guid.NewGuid())),
            services =>
            {
                services.AddSingleton(auditWriter);
                services.AddSingleton<IMiniBusAuditWriter>(auditWriter);
                services.AddSingleton(recorder);
                services.AddSingleton<IHandleMessages<TestCommand>, RecordingCommandHandler>();
            },
            options =>
            {
                options.Audit.AuditIdFactory = () => "audit-1";
                options.Audit.UtcNowProvider = () => auditedUtc;
            });
        var message = CreateMessage(new Dictionary<string, object>
        {
            [MiniBusHeaderNames.MessageType] = typeof(TestCommand).AssemblyQualifiedName!,
            [MiniBusHeaderNames.MessageId] = "message-1",
            [MiniBusHeaderNames.CorrelationId] = "correlation-1"
        });

        await processor.ProcessAsync(message, actions);

        var record = Assert.Single(auditWriter.Records);
        Assert.Equal(MiniBusAuditProcessingOutcome.Completed, record.Outcome);
        Assert.Equal("Billing", record.EndpointName);
        Assert.Equal("message-1", record.MessageId);
        Assert.Equal("correlation-1", record.CorrelationId);
        Assert.Equal("audit-1", record.AuditId);
        Assert.Equal(auditedUtc, record.AuditedUtc);
        Assert.Equal(typeof(TestCommand).AssemblyQualifiedName, record.MessageType);
        Assert.NotNull(record.Body);
        Assert.Same(message, actions.CompletedMessage);
    }

    [Fact]
    public async Task ProcessAsync_WithoutSettlementWithAuditWriterAuditsSuccess()
    {
        var recorder = new HandlerRecorder();
        var auditWriter = new RecordingAuditWriter();
        var processor = CreateProcessor(new RecordingSerializer(new TestCommand(Guid.NewGuid())), services =>
        {
            services.AddSingleton(auditWriter);
            services.AddSingleton<IMiniBusAuditWriter>(auditWriter);
            services.AddSingleton(recorder);
            services.AddSingleton<IHandleMessages<TestCommand>, RecordingCommandHandler>();
        });
        var message = CreateMessage(new Dictionary<string, object>
        {
            [MiniBusHeaderNames.MessageType] = typeof(TestCommand).AssemblyQualifiedName!,
            [MiniBusHeaderNames.MessageId] = "message-1"
        });

        await processor.ProcessAsync(message);

        var record = Assert.Single(auditWriter.Records);
        Assert.Equal(MiniBusAuditProcessingOutcome.Completed, record.Outcome);
        Assert.Equal("message-1", record.MessageId);
    }

    [Fact]
    public async Task ProcessAsync_WithAuditWriterAuditsDuplicateBeforeCompletion()
    {
        var auditWriter = new RecordingAuditWriter();
        var session = new RecordingPersistenceSession { IsProcessed = true };
        var actions = new RecordingMessageActions();
        auditWriter.OnWrite = () => Assert.Null(actions.CompletedMessage);
        var processor = CreateProcessor(new RecordingSerializer(new TestCommand(Guid.NewGuid())), services =>
        {
            services.AddSingleton(auditWriter);
            services.AddSingleton<IMiniBusAuditWriter>(auditWriter);
            services.AddSingleton(new HandlerRecorder());
            services.AddSingleton<IHandleMessages<TestCommand>, RecordingCommandHandler>();
            services.AddSingleton<IMiniBusPersistenceSessionFactory>(new RecordingPersistenceSessionFactory(session));
        });
        var message = CreateMessage(new Dictionary<string, object>
        {
            [MiniBusHeaderNames.MessageType] = typeof(TestCommand).AssemblyQualifiedName!,
            [MiniBusRecoverabilityHeaderNames.OriginalMessageId] = "original-message"
        });

        await processor.ProcessAsync(message, actions);

        var record = Assert.Single(auditWriter.Records);
        Assert.Equal(MiniBusAuditProcessingOutcome.SkippedDuplicate, record.Outcome);
        Assert.Equal("original-message", record.Headers[MiniBusRecoverabilityHeaderNames.OriginalMessageId]);
        Assert.Same(message, actions.CompletedMessage);
    }

    [Fact]
    public async Task ProcessAsync_WithAuditWriterAuditsDelayedRetryBeforeCompletion()
    {
        var recorder = new HandlerRecorder();
        var sender = new RecordingSender();
        var auditWriter = new RecordingAuditWriter();
        var actions = new RecordingMessageActions();
        auditWriter.OnWrite = () => Assert.Null(actions.CompletedMessage);
        var processor = CreateProcessor(
            new RecordingSerializer(new TestCommand(Guid.NewGuid())),
            services =>
            {
                services.AddSingleton(auditWriter);
                services.AddSingleton<IMiniBusAuditWriter>(auditWriter);
                services.AddSingleton(recorder);
                services.AddSingleton<IHandleMessages<TestCommand>, RecordingThenThrowingCommandHandler>();
                RegisterTransport(services, sender, routes => routes.MapCommand<TestCommand>("billing-queue"));
            },
            options =>
            {
                options.Recoverability.ImmediateRetries = 0;
                options.Recoverability.DelayedRetries.Add(TimeSpan.FromSeconds(10));
            });
        var message = CreateMessage(new Dictionary<string, object>
        {
            [MiniBusHeaderNames.MessageType] = typeof(TestCommand).AssemblyQualifiedName!,
            [MiniBusHeaderNames.MessageId] = "message-1"
        });

        await processor.ProcessAsync(message, actions);

        var record = Assert.Single(auditWriter.Records);
        Assert.Equal(MiniBusAuditProcessingOutcome.DelayedRetryScheduled, record.Outcome);
        Assert.Equal("1", record.RecoverabilityMetadata[MiniBusRecoverabilityHeaderNames.DelayedAttempt]);
        Assert.Single(sender.Schedules);
        Assert.Same(message, actions.CompletedMessage);
    }

    [Fact]
    public async Task ProcessAsync_WithAuditWriterAuditsDeadLetterBeforeSettlement()
    {
        var auditWriter = new RecordingAuditWriter();
        var actions = new RecordingMessageActions();
        auditWriter.OnWrite = () => Assert.Null(actions.DeadLetteredMessage);
        var processor = CreateProcessor(new RecordingSerializer(new TestCommand(Guid.NewGuid())), services =>
        {
            services.AddSingleton(auditWriter);
            services.AddSingleton<IMiniBusAuditWriter>(auditWriter);
            services.AddSingleton<IHandleMessages<TestCommand>, ThrowingCommandHandler>();
        });
        var message = CreateMessage(new Dictionary<string, object>
        {
            [MiniBusHeaderNames.MessageType] = typeof(TestCommand).AssemblyQualifiedName!
        });

        await processor.ProcessAsync(message, actions);

        var record = Assert.Single(auditWriter.Records);
        Assert.Equal(MiniBusAuditProcessingOutcome.DeadLettered, record.Outcome);
        Assert.Equal(RecoverabilityDecisionMaker.RetriesExhaustedDeadLetterReason, record.DeadLetterReason);
        Assert.Contains("handler failed", record.DeadLetterDescription, StringComparison.Ordinal);
        Assert.Same(message, actions.DeadLetteredMessage);
    }

    [Fact]
    public async Task ProcessAsync_WithAuditWriterStoresClaimCheckMetadataWithoutResolvedBodyByDefault()
    {
        var recorder = new HandlerRecorder();
        var auditWriter = new RecordingAuditWriter();
        var store = new RecordingClaimCheckPayloadStore
        {
            Payload = BinaryData.FromString("{\"id\":\"restored\"}")
        };
        var processor = CreateProcessor(new RecordingSerializer(new TestCommand(Guid.NewGuid())), services =>
        {
            services.AddSingleton(auditWriter);
            services.AddSingleton<IMiniBusAuditWriter>(auditWriter);
            services.AddSingleton(recorder);
            services.AddSingleton<IHandleMessages<TestCommand>, RecordingCommandHandler>();
            services.AddSingleton<IMiniBusClaimCheckPayloadStore>(store);
        });

        await processor.ProcessAsync(CreateMessage(CreateClaimCheckProperties()), new RecordingMessageActions());

        var record = Assert.Single(auditWriter.Records);
        Assert.Null(record.Body);
        Assert.NotNull(record.ClaimCheck);
        Assert.Equal("payloads/payload-1.bin", record.ClaimCheck.BlobName);
    }

    [Fact]
    public async Task ProcessAsync_WithAuditFailureDoesNotSettleMessage()
    {
        var auditWriter = new RecordingAuditWriter
        {
            Exception = new InvalidOperationException("audit failed")
        };
        var processor = CreateProcessor(new RecordingSerializer(new TestCommand(Guid.NewGuid())), services =>
        {
            services.AddSingleton(auditWriter);
            services.AddSingleton<IMiniBusAuditWriter>(auditWriter);
            services.AddSingleton(new HandlerRecorder());
            services.AddSingleton<IHandleMessages<TestCommand>, RecordingCommandHandler>();
        });
        var message = CreateMessage(new Dictionary<string, object>
        {
            [MiniBusHeaderNames.MessageType] = typeof(TestCommand).AssemblyQualifiedName!
        });
        var actions = new RecordingMessageActions();

        var exception = await Assert.ThrowsAsync<MiniBusAuditWriteException>(
            () => processor.ProcessAsync(message, actions));

        Assert.Equal("audit failed", exception.InnerException?.Message);
        Assert.Null(actions.CompletedMessage);
        Assert.Null(actions.DeadLetteredMessage);
    }

    [Fact]
    public async Task ProcessAsync_WithAuditWriterFailsWhenRequiredAuditMetadataIsMissing()
    {
        var auditWriter = new RecordingAuditWriter();
        var processor = CreateProcessor(new RecordingSerializer(new TestCommand(Guid.NewGuid())), services =>
        {
            services.AddSingleton(auditWriter);
            services.AddSingleton<IMiniBusAuditWriter>(auditWriter);
            services.AddSingleton(new HandlerRecorder());
            services.AddSingleton<IHandleMessages<TestCommand>, RecordingCommandHandler>();
        });
        var message = ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromString("{}"),
            messageId: null,
            correlationId: "sdk-correlation-id",
            properties: new Dictionary<string, object>
            {
                [MiniBusHeaderNames.MessageType] = typeof(TestCommand).AssemblyQualifiedName!
            });
        var actions = new RecordingMessageActions();

        var exception = await Assert.ThrowsAsync<MiniBusAuditWriteException>(
            () => processor.ProcessAsync(message, actions));

        Assert.Contains(MiniBusHeaderNames.MessageId, exception.InnerException?.Message, StringComparison.Ordinal);
        Assert.Empty(auditWriter.Records);
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

    private static string? GetTag(
        Activity activity,
        string key)
    {
        return activity.Tags
            .Where(tag => string.Equals(tag.Key, key, StringComparison.Ordinal))
            .Select(tag => tag.Value)
            .FirstOrDefault();
    }

    private static object? GetTagObject(
        Activity activity,
        string key)
    {
        return activity.TagObjects
            .Where(tag => string.Equals(tag.Key, key, StringComparison.Ordinal))
            .Select(tag => tag.Value)
            .FirstOrDefault();
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

    private sealed record CompleteBillingSaga(string BillingId) : ICommand;

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

    private sealed class CompletingSaga :
        MiniBusSaga<BillingSagaData>,
        IHandleSagaMessages<CompleteBillingSaga>
    {
        public override void ConfigureHowToFindSaga(SagaMapper<BillingSagaData> mapper)
        {
            mapper.StartsWith<CompleteBillingSaga>(message => message.BillingId);
        }

        public Task Handle(CompleteBillingSaga message, MiniBusContext context, CancellationToken cancellationToken)
        {
            Data.IsCompleted = true;
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

    private sealed class SecondRecordingCommandHandler : IHandleMessages<TestCommand>
    {
        private readonly HandlerRecorder _recorder;

        public SecondRecordingCommandHandler(HandlerRecorder recorder)
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

    private sealed class RecordingAuditWriter : IMiniBusAuditWriter
    {
        public List<MiniBusAuditRecord> Records { get; } = new();

        public Exception? Exception { get; init; }

        public Action? OnWrite { get; set; }

        public Task WriteAsync(
            MiniBusAuditRecord record,
            CancellationToken cancellationToken = default)
        {
            OnWrite?.Invoke();

            if (Exception is not null)
            {
                return Task.FromException(Exception);
            }

            Records.Add(record);
            return Task.CompletedTask;
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

    private sealed class RecordingPersistenceSession : IMiniBusPersistenceSession, ISagaPersistence
    {
        private readonly InMemorySagaPersistence _sagaPersistence = new();

        public bool IsProcessed { get; init; }

        public MiniBusInboxMessage? CheckedMessage { get; private set; }

        public MiniBusInboxMessage? CommittedMessage { get; private set; }

        public List<MiniBusOutboxOperation> CommittedOperations { get; } = new();

        public Exception? CommitException { get; init; }

        public Action? OnCommit { get; set; }

        public int LoadCount { get; private set; }

        public int CreateCount { get; private set; }

        public int SaveCount { get; private set; }

        public int CompleteCount { get; private set; }

        public Task<bool> TryBeginAsync(
            MiniBusInboxMessage message,
            CancellationToken cancellationToken = default)
        {
            CheckedMessage = message;
            return Task.FromResult(!IsProcessed);
        }

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

        public Task<SagaPersistenceRecord<TData>?> LoadAsync<TData>(
            string correlationId,
            CancellationToken cancellationToken = default)
            where TData : class, ISagaData, new()
        {
            LoadCount++;
            return _sagaPersistence.LoadAsync<TData>(correlationId, cancellationToken);
        }

        public Task CreateAsync<TData>(
            TData data,
            CancellationToken cancellationToken = default)
            where TData : class, ISagaData, new()
        {
            CreateCount++;
            return _sagaPersistence.CreateAsync(data, cancellationToken);
        }

        public Task SaveAsync<TData>(
            TData data,
            string version,
            CancellationToken cancellationToken = default)
            where TData : class, ISagaData, new()
        {
            SaveCount++;
            return _sagaPersistence.SaveAsync(data, version, cancellationToken);
        }

        public Task CompleteAsync<TData>(
            TData data,
            string version,
            CancellationToken cancellationToken = default)
            where TData : class, ISagaData, new()
        {
            CompleteCount++;
            return _sagaPersistence.CompleteAsync(data, version, cancellationToken);
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

    private sealed class RecordingMeterListener : IDisposable
    {
        private readonly HashSet<string> _meterNames;
        private readonly MeterListener _listener = new();

        public RecordingMeterListener(params string[] meterNames)
        {
            _meterNames = new HashSet<string>(meterNames, StringComparer.Ordinal);
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (_meterNames.Contains(instrument.Meter.Name))
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            };
            _listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
                Measurements.Add(RecordedMeasurement.Create(instrument, measurement, tags)));
            _listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, _) =>
                Measurements.Add(RecordedMeasurement.Create(instrument, measurement, tags)));
            _listener.Start();
        }

        public List<RecordedMeasurement> Measurements { get; } = new();

        public void Dispose()
        {
            _listener.Dispose();
        }
    }

    private sealed record RecordedMeasurement(
        string MeterName,
        string InstrumentName,
        string? Unit,
        object Value,
        IReadOnlyDictionary<string, object?> Tags)
    {
        public static RecordedMeasurement Create<T>(
            Instrument instrument,
            T value,
            ReadOnlySpan<KeyValuePair<string, object?>> tags)
        {
            return new RecordedMeasurement(
                instrument.Meter.Name,
                instrument.Name,
                instrument.Unit,
                value!,
                tags.ToArray().ToDictionary(tag => tag.Key, tag => tag.Value, StringComparer.Ordinal));
        }
    }
}
