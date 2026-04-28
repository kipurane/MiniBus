using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using MiniBus.Core.Context;
using MiniBus.Core.Contracts;
using MiniBus.Core.Handlers;
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

