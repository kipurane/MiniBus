using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using MiniBus.Core.Context;
using MiniBus.Core.Contracts;
using MiniBus.Core.Handlers;
using MiniBus.Core.Recoverability;
using MiniBus.Core.Routing;
using MiniBus.Core.Serialization;
using Xunit;

namespace MiniBus.Core.Tests;

public sealed class CoreMessageProcessingTests
{
    [Fact]
    public void MessageContractsExpressMessageIntent()
    {
        Assert.IsAssignableFrom<IMessage>(new TestMessage(Guid.NewGuid()));
        Assert.IsAssignableFrom<ICommand>(new TestCommand(Guid.NewGuid()));
        Assert.IsAssignableFrom<IEvent>(new TestEvent(Guid.NewGuid()));
        Assert.IsAssignableFrom<IHandleMessages<TestCommand>>(new RecordingCommandHandler(new InvocationRecorder()));
    }

    [Fact]
    public void SystemTextJsonSerializer_RoundTripsMessageUsingExplicitType()
    {
        var serializer = new SystemTextJsonMessageSerializer();
        var original = new TestCommand(Guid.NewGuid());

        var body = serializer.Serialize(original, typeof(TestCommand));
        var deserialized = serializer.Deserialize(body, typeof(TestCommand));

        var roundTripped = Assert.IsType<TestCommand>(deserialized);
        Assert.Equal(original, roundTripped);
    }

    [Fact]
    public void CommandRouteRegistry_ResolvesConfiguredDestination()
    {
        var routes = new CommandRouteRegistry();
        routes.Map<TestCommand>("billing-queue");

        var destination = routes.GetDestination<TestCommand>();

        Assert.Equal("billing-queue", destination);
    }

    [Fact]
    public void CommandRouteRegistry_ThrowsWhenRouteIsMissing()
    {
        var routes = new CommandRouteRegistry();

        var exception = Assert.Throws<CommandRouteNotFoundException>(() => routes.GetDestination<TestCommand>());

        Assert.Equal(typeof(TestCommand), exception.CommandType);
    }

    [Fact]
    public void CommandRouteRegistry_ThrowsWhenConflictingRouteIsRegistered()
    {
        var routes = new CommandRouteRegistry();
        routes.Map<TestCommand>("billing-queue");

        var exception = Assert.Throws<CommandRouteConflictException>(() => routes.Map<TestCommand>("inventory-queue"));

        Assert.Equal(typeof(TestCommand), exception.CommandType);
        Assert.Equal("billing-queue", exception.ExistingDestination);
        Assert.Equal("inventory-queue", exception.AttemptedDestination);
    }

    [Fact]
    public void HandlerDiscovery_FindsConcreteHandlersAndImplementedContracts()
    {
        var registrations = HandlerDiscovery.Discover(Assembly.GetExecutingAssembly());

        Assert.Contains(registrations, registration =>
            registration.HandlerType == typeof(RecordingCommandHandler)
            && registration.ServiceType == typeof(IHandleMessages<TestCommand>)
            && registration.MessageType == typeof(TestCommand));

        Assert.Contains(registrations, registration =>
            registration.HandlerType == typeof(MultiHandler)
            && registration.ServiceType == typeof(IHandleMessages<TestCommand>)
            && registration.MessageType == typeof(TestCommand));

        Assert.Contains(registrations, registration =>
            registration.HandlerType == typeof(MultiHandler)
            && registration.ServiceType == typeof(IHandleMessages<TestEvent>)
            && registration.MessageType == typeof(TestEvent));
    }

    [Fact]
    public void HandlerDiscovery_IgnoresAbstractAndNonHandlerTypes()
    {
        var registrations = HandlerDiscovery.Discover(Assembly.GetExecutingAssembly());

        Assert.DoesNotContain(registrations, registration => registration.HandlerType == typeof(AbstractCommandHandler));
        Assert.DoesNotContain(registrations, registration => registration.HandlerType == typeof(PlainType));
    }

    [Fact]
    public async Task MessageHandlerInvoker_InvokesASingleHandler()
    {
        var recorder = new InvocationRecorder();
        var serviceProvider = new ServiceCollection()
            .AddSingleton(recorder)
            .AddTransient<IHandleMessages<TestCommand>, RecordingCommandHandler>()
            .BuildServiceProvider();
        var invoker = new MessageHandlerInvoker();
        var context = new RecordingMiniBusContext();
        var message = new TestCommand(Guid.NewGuid());
        using var cancellationTokenSource = new CancellationTokenSource();

        await invoker.InvokeAsync(message, context, serviceProvider, cancellationTokenSource.Token);

        Assert.Single(recorder.Invocations);
        Assert.Equal(message, recorder.Invocations[0].Message);
        Assert.Same(context, recorder.Invocations[0].Context);
        Assert.Equal(cancellationTokenSource.Token, recorder.Invocations[0].CancellationToken);
    }

    [Fact]
    public async Task MessageHandlerInvoker_InvokesAllRegisteredHandlers()
    {
        var recorder = new InvocationRecorder();
        var serviceProvider = new ServiceCollection()
            .AddSingleton(recorder)
            .AddTransient<IHandleMessages<TestCommand>, RecordingCommandHandler>()
            .AddTransient<IHandleMessages<TestCommand>, SecondaryCommandHandler>()
            .BuildServiceProvider();
        var invoker = new MessageHandlerInvoker();
        var context = new RecordingMiniBusContext();
        var message = new TestCommand(Guid.NewGuid());

        await invoker.InvokeAsync(message, context, serviceProvider, CancellationToken.None);

        Assert.Equal(2, recorder.Invocations.Count);
        Assert.Contains(recorder.Invocations, invocation => invocation.HandlerName == nameof(RecordingCommandHandler));
        Assert.Contains(recorder.Invocations, invocation => invocation.HandlerName == nameof(SecondaryCommandHandler));
    }

    [Fact]
    public async Task MessageHandlerInvoker_CompletesWhenNoHandlersAreRegistered()
    {
        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var invoker = new MessageHandlerInvoker();

        await invoker.InvokeAsync(new TestCommand(Guid.NewGuid()), new RecordingMiniBusContext(), serviceProvider, CancellationToken.None);
    }

    [Fact]
    public void RecoverabilityDecisionMaker_ReturnsImmediateRetryWhenAttemptsRemain()
    {
        var options = new MiniBusRecoverabilityOptions { ImmediateRetries = 2 };
        var decisionMaker = new RecoverabilityDecisionMaker();

        var decision = decisionMaker.Decide(
            new Dictionary<string, string>(StringComparer.Ordinal),
            options,
            new InvalidOperationException("handler failed"),
            "transport-message-1");

        Assert.Equal(RecoverabilityDecisionKind.ImmediateRetry, decision.Kind);
        Assert.Equal(1, decision.ImmediateAttempt);
        Assert.Equal("1", decision.Headers[MiniBusRecoverabilityHeaderNames.ImmediateAttempt]);
        Assert.Equal("2", decision.Headers[MiniBusRecoverabilityHeaderNames.MaxImmediateAttempts]);
        Assert.Equal("transport-message-1", decision.Headers[MiniBusRecoverabilityHeaderNames.OriginalMessageId]);
        Assert.Equal(typeof(InvalidOperationException).FullName, decision.Headers[MiniBusRecoverabilityHeaderNames.ExceptionType]);
        Assert.Equal("handler failed", decision.Headers[MiniBusRecoverabilityHeaderNames.ExceptionMessage]);
    }

    [Fact]
    public void RecoverabilityDecisionMaker_ReturnsDelayedRetryWhenImmediateRetriesAreExhausted()
    {
        var options = new MiniBusRecoverabilityOptions { ImmediateRetries = 1 };
        options.DelayedRetries.Add(TimeSpan.FromSeconds(10));
        options.DelayedRetries.Add(TimeSpan.FromMinutes(1));
        var headers = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [MiniBusRecoverabilityHeaderNames.ImmediateAttempt] = "1"
        };
        var decisionMaker = new RecoverabilityDecisionMaker();

        var decision = decisionMaker.Decide(
            headers,
            options,
            new InvalidOperationException("handler failed"),
            "transport-message-1");

        Assert.Equal(RecoverabilityDecisionKind.DelayedRetry, decision.Kind);
        Assert.Equal(TimeSpan.FromSeconds(10), decision.Delay);
        Assert.Equal(0, decision.ImmediateAttempt);
        Assert.Equal(1, decision.DelayedAttempt);
        Assert.Equal("0", decision.Headers[MiniBusRecoverabilityHeaderNames.ImmediateAttempt]);
        Assert.Equal("1", decision.Headers[MiniBusRecoverabilityHeaderNames.DelayedAttempt]);
        Assert.Equal("2", decision.Headers[MiniBusRecoverabilityHeaderNames.MaxDelayedAttempts]);
    }

    [Fact]
    public void RecoverabilityDecisionMaker_ReturnsDelayedRetryWhenImmediateRetriesIsZero()
    {
        var options = new MiniBusRecoverabilityOptions { ImmediateRetries = 0 };
        options.DelayedRetries.Add(TimeSpan.FromSeconds(10));
        var decisionMaker = new RecoverabilityDecisionMaker();

        var decision = decisionMaker.Decide(
            new Dictionary<string, string>(StringComparer.Ordinal),
            options,
            new InvalidOperationException("handler failed"),
            "transport-message-1");

        Assert.Equal(RecoverabilityDecisionKind.DelayedRetry, decision.Kind);
        Assert.Equal(TimeSpan.FromSeconds(10), decision.Delay);
        Assert.Equal(0, decision.ImmediateAttempt);
        Assert.Equal(1, decision.DelayedAttempt);
        Assert.Equal("0", decision.Headers[MiniBusRecoverabilityHeaderNames.ImmediateAttempt]);
        Assert.Equal("1", decision.Headers[MiniBusRecoverabilityHeaderNames.DelayedAttempt]);
        Assert.Equal("0", decision.Headers[MiniBusRecoverabilityHeaderNames.MaxImmediateAttempts]);
        Assert.Equal("1", decision.Headers[MiniBusRecoverabilityHeaderNames.MaxDelayedAttempts]);
    }

    [Fact]
    public void RecoverabilityDecisionMaker_ReturnsDeadLetterWhenRetriesAreExhausted()
    {
        var options = new MiniBusRecoverabilityOptions { ImmediateRetries = 1 };
        options.DelayedRetries.Add(TimeSpan.FromSeconds(10));
        var headers = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [MiniBusRecoverabilityHeaderNames.ImmediateAttempt] = "1",
            [MiniBusRecoverabilityHeaderNames.DelayedAttempt] = "1",
            [MiniBusRecoverabilityHeaderNames.OriginalMessageId] = "original-message-1"
        };
        var decisionMaker = new RecoverabilityDecisionMaker();

        var decision = decisionMaker.Decide(
            headers,
            options,
            new InvalidOperationException("handler failed"),
            "transport-message-1");

        Assert.Equal(RecoverabilityDecisionKind.DeadLetter, decision.Kind);
        Assert.Equal(RecoverabilityDecisionMaker.RetriesExhaustedDeadLetterReason, decision.DeadLetterReason);
        Assert.Contains("ExceptionType=System.InvalidOperationException", decision.DeadLetterDescription, StringComparison.Ordinal);
        Assert.Contains("ExceptionMessage=handler failed", decision.DeadLetterDescription, StringComparison.Ordinal);
        Assert.Contains("ImmediateAttempt=1", decision.DeadLetterDescription, StringComparison.Ordinal);
        Assert.Contains("DelayedAttempt=1", decision.DeadLetterDescription, StringComparison.Ordinal);
        Assert.Contains("OriginalMessageId=original-message-1", decision.DeadLetterDescription, StringComparison.Ordinal);
    }

    [Fact]
    public void RecoverabilityDecisionMaker_ThrowsWhenImmediateRetriesIsNegative()
    {
        var options = new MiniBusRecoverabilityOptions { ImmediateRetries = -1 };
        var decisionMaker = new RecoverabilityDecisionMaker();

        Assert.Throws<ArgumentOutOfRangeException>(() => decisionMaker.Decide(
            new Dictionary<string, string>(StringComparer.Ordinal),
            options,
            new InvalidOperationException("handler failed"),
            "transport-message-1"));
    }

    [Fact]
    public void RecoverabilityDecisionMaker_PropagatesWhenDeadLetterIsDisabled()
    {
        var options = new MiniBusRecoverabilityOptions
        {
            ImmediateRetries = 0,
            DeadLetterAfterRetriesExhausted = false
        };
        var decisionMaker = new RecoverabilityDecisionMaker();

        var decision = decisionMaker.Decide(
            new Dictionary<string, string>(StringComparer.Ordinal),
            options,
            new InvalidOperationException("handler failed"),
            "transport-message-1");

        Assert.Equal(RecoverabilityDecisionKind.Propagate, decision.Kind);
        Assert.Null(decision.DeadLetterReason);
        Assert.Null(decision.DeadLetterDescription);
    }

    [Fact]
    public void RecoverabilityDecisionMaker_PreservesExistingOriginalMessageId()
    {
        var options = new MiniBusRecoverabilityOptions { ImmediateRetries = 1 };
        var headers = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [MiniBusRecoverabilityHeaderNames.OriginalMessageId] = "the-original-message"
        };
        var decisionMaker = new RecoverabilityDecisionMaker();

        var decision = decisionMaker.Decide(
            headers,
            options,
            new InvalidOperationException("handler failed"),
            "current-transport-message");

        Assert.Equal("the-original-message", decision.Headers[MiniBusRecoverabilityHeaderNames.OriginalMessageId]);
    }

    [Fact]
    public void RecoverabilityDecisionMaker_ThrowsWhenHeadersIsNull()
    {
        var options = new MiniBusRecoverabilityOptions { ImmediateRetries = 1 };
        var decisionMaker = new RecoverabilityDecisionMaker();

        Assert.Throws<ArgumentNullException>(() => decisionMaker.Decide(
            null!,
            options,
            new InvalidOperationException("handler failed"),
            "transport-message-1"));
    }

    [Fact]
    public void RecoverabilityDecisionMaker_ThrowsWhenOptionsIsNull()
    {
        var decisionMaker = new RecoverabilityDecisionMaker();

        Assert.Throws<ArgumentNullException>(() => decisionMaker.Decide(
            new Dictionary<string, string>(StringComparer.Ordinal),
            null!,
            new InvalidOperationException("handler failed"),
            "transport-message-1"));
    }

    [Fact]
    public void RecoverabilityDecisionMaker_ThrowsWhenExceptionIsNull()
    {
        var options = new MiniBusRecoverabilityOptions { ImmediateRetries = 1 };
        var decisionMaker = new RecoverabilityDecisionMaker();

        Assert.Throws<ArgumentNullException>(() => decisionMaker.Decide(
            new Dictionary<string, string>(StringComparer.Ordinal),
            options,
            null!,
            "transport-message-1"));
    }

    [Fact]
    public void RecoverabilityDecisionMaker_ProgressesThroughMultipleDelayedRetries()
    {
        var options = new MiniBusRecoverabilityOptions { ImmediateRetries = 0 };
        options.DelayedRetries.Add(TimeSpan.FromSeconds(10));
        options.DelayedRetries.Add(TimeSpan.FromMinutes(1));
        options.DelayedRetries.Add(TimeSpan.FromMinutes(5));
        var decisionMaker = new RecoverabilityDecisionMaker();

        var firstDelayed = decisionMaker.Decide(
            new Dictionary<string, string>(StringComparer.Ordinal),
            options,
            new InvalidOperationException("fail"),
            "msg-1");
        Assert.Equal(RecoverabilityDecisionKind.DelayedRetry, firstDelayed.Kind);
        Assert.Equal(TimeSpan.FromSeconds(10), firstDelayed.Delay);
        Assert.Equal(1, firstDelayed.DelayedAttempt);

        var secondDelayed = decisionMaker.Decide(
            new Dictionary<string, string>(firstDelayed.Headers, StringComparer.Ordinal),
            options,
            new InvalidOperationException("fail"),
            "msg-1");
        Assert.Equal(RecoverabilityDecisionKind.DelayedRetry, secondDelayed.Kind);
        Assert.Equal(TimeSpan.FromMinutes(1), secondDelayed.Delay);
        Assert.Equal(2, secondDelayed.DelayedAttempt);

        var thirdDelayed = decisionMaker.Decide(
            new Dictionary<string, string>(secondDelayed.Headers, StringComparer.Ordinal),
            options,
            new InvalidOperationException("fail"),
            "msg-1");
        Assert.Equal(RecoverabilityDecisionKind.DelayedRetry, thirdDelayed.Kind);
        Assert.Equal(TimeSpan.FromMinutes(5), thirdDelayed.Delay);
        Assert.Equal(3, thirdDelayed.DelayedAttempt);

        var deadLetter = decisionMaker.Decide(
            new Dictionary<string, string>(thirdDelayed.Headers, StringComparer.Ordinal),
            options,
            new InvalidOperationException("fail"),
            "msg-1");
        Assert.Equal(RecoverabilityDecisionKind.DeadLetter, deadLetter.Kind);
    }

    [Fact]
    public void RecoverabilityDecisionMaker_TruncatesDeadLetterDescriptionWhenTooLong()
    {
        var options = new MiniBusRecoverabilityOptions { ImmediateRetries = 0 };
        var longMessage = new string('x', 5000);
        var decisionMaker = new RecoverabilityDecisionMaker();

        var decision = decisionMaker.Decide(
            new Dictionary<string, string>(StringComparer.Ordinal),
            options,
            new InvalidOperationException(longMessage),
            "transport-message-1");

        Assert.Equal(RecoverabilityDecisionKind.DeadLetter, decision.Kind);
        Assert.NotNull(decision.DeadLetterDescription);
        Assert.True(decision.DeadLetterDescription!.Length <= 4096);
    }

    [Fact]
    public void CommandRouteRegistry_MapByType_RegistersRoute()
    {
        var routes = new CommandRouteRegistry();
        routes.Map(typeof(TestCommand), "billing-queue");

        Assert.Equal("billing-queue", routes.GetDestination(typeof(TestCommand)));
    }

    [Fact]
    public void CommandRouteRegistry_MapByType_ThrowsWhenCommandTypeIsNull()
    {
        var routes = new CommandRouteRegistry();

        Assert.Throws<ArgumentNullException>(() => routes.Map(null!, "billing-queue"));
    }

    [Fact]
    public void CommandRouteRegistry_MapByType_ThrowsWhenDestinationIsEmpty()
    {
        var routes = new CommandRouteRegistry();

        Assert.Throws<ArgumentException>(() => routes.Map(typeof(TestCommand), ""));
        Assert.Throws<ArgumentException>(() => routes.Map(typeof(TestCommand), "   "));
    }

    [Fact]
    public void CommandRouteRegistry_MapByType_ThrowsWhenTypeDoesNotImplementICommand()
    {
        var routes = new CommandRouteRegistry();

        Assert.Throws<ArgumentException>(() => routes.Map(typeof(TestMessage), "billing-queue"));
    }

    [Fact]
    public void CommandRouteRegistry_MapByType_AllowsIdempotentRegistrationOfSameDestination()
    {
        var routes = new CommandRouteRegistry();
        routes.Map(typeof(TestCommand), "billing-queue");

        routes.Map(typeof(TestCommand), "billing-queue");

        Assert.Equal("billing-queue", routes.GetDestination(typeof(TestCommand)));
    }

    [Fact]
    public void CommandRouteRegistry_GetDestinationByType_ThrowsWhenCommandTypeIsNull()
    {
        var routes = new CommandRouteRegistry();

        Assert.Throws<ArgumentNullException>(() => routes.GetDestination(null!));
    }

    [Fact]
    public void HandlerDiscovery_DeduplicatesAcrossDuplicateAssemblies()
    {
        var assembly = System.Reflection.Assembly.GetExecutingAssembly();

        var registrations = HandlerDiscovery.Discover(assembly, assembly);

        var commandHandlerRegistrations = registrations
            .Where(r => r.HandlerType == typeof(RecordingCommandHandler))
            .ToList();

        Assert.Single(commandHandlerRegistrations);
    }

    [Fact]
    public void HandlerDiscovery_ReturnsEmptyForEmptyAssemblyList()
    {
        var registrations = HandlerDiscovery.Discover(Array.Empty<System.Reflection.Assembly>());

        Assert.Empty(registrations);
    }

    private sealed record TestMessage(Guid Id) : IMessage;

    private sealed record TestCommand(Guid Id) : ICommand;

    private sealed record TestEvent(Guid Id) : IEvent;

    private sealed class RecordingMiniBusContext : MiniBusContext
    {
        private readonly Dictionary<string, string> _headers = new(StringComparer.Ordinal);

        public override string EndpointName => "Tests";

        public override string MessageId => "message-id";

        public override string CorrelationId => "correlation-id";

        public override string? CausationId => "causation-id";

        public override IReadOnlyDictionary<string, string> Headers => _headers;

        public List<object> SentMessages { get; } = new();

        public List<object> PublishedMessages { get; } = new();

        public List<(object Message, DateTimeOffset DueTime)> ScheduledMessages { get; } = new();

        public override Task Send<TCommand>(TCommand command, CancellationToken cancellationToken = default)
        {
            SentMessages.Add(command!);
            return Task.CompletedTask;
        }

        public override Task Publish<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        {
            PublishedMessages.Add(@event!);
            return Task.CompletedTask;
        }

        public override Task Schedule<TMessage>(TMessage message, DateTimeOffset dueTime, CancellationToken cancellationToken = default)
        {
            ScheduledMessages.Add((message!, dueTime));
            return Task.CompletedTask;
        }
    }

    private sealed class InvocationRecorder
    {
        public List<InvocationRecord> Invocations { get; } = new();

        public void Record(string handlerName, object message, MiniBusContext context, CancellationToken cancellationToken)
        {
            Invocations.Add(new InvocationRecord(handlerName, message, context, cancellationToken));
        }
    }

    private sealed record InvocationRecord(string HandlerName, object Message, MiniBusContext Context, CancellationToken CancellationToken);

    private sealed class RecordingCommandHandler : IHandleMessages<TestCommand>
    {
        private readonly InvocationRecorder _recorder;

        public RecordingCommandHandler(InvocationRecorder recorder)
        {
            _recorder = recorder;
        }

        public Task Handle(TestCommand message, MiniBusContext context, CancellationToken cancellationToken)
        {
            _recorder.Record(nameof(RecordingCommandHandler), message, context, cancellationToken);
            return Task.CompletedTask;
        }
    }

    private sealed class SecondaryCommandHandler : IHandleMessages<TestCommand>
    {
        private readonly InvocationRecorder _recorder;

        public SecondaryCommandHandler(InvocationRecorder recorder)
        {
            _recorder = recorder;
        }

        public Task Handle(TestCommand message, MiniBusContext context, CancellationToken cancellationToken)
        {
            _recorder.Record(nameof(SecondaryCommandHandler), message, context, cancellationToken);
            return Task.CompletedTask;
        }
    }

    private sealed class MultiHandler :
        IHandleMessages<TestCommand>,
        IHandleMessages<TestEvent>
    {
        public Task Handle(TestCommand message, MiniBusContext context, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task Handle(TestEvent message, MiniBusContext context, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private abstract class AbstractCommandHandler : IHandleMessages<TestCommand>
    {
        public abstract Task Handle(TestCommand message, MiniBusContext context, CancellationToken cancellationToken);
    }

    private sealed class PlainType;
}
