using MiniBus.Core.Context;
using MiniBus.Core.Contracts;
using MiniBus.Core.Sagas;
using Xunit;

namespace MiniBus.Core.Tests;

public sealed class SagaTests
{
    [Fact]
    public void SagaContracts_AttachDataAndMarkComplete()
    {
        var saga = new OrderSaga();
        var data = new OrderSagaData { Id = Guid.NewGuid(), CorrelationId = "order-1" };

        saga.AttachForTest(data);
        saga.MarkAsComplete();

        Assert.Same(data, saga.Data);
        Assert.True(data.IsCompleted);
    }

    [Fact]
    public void SagaPersistenceException_PreservesInnerException()
    {
        var inner = new InvalidOperationException("provider failure");

        var exception = new SagaPersistenceException("saga persistence failed", inner);

        Assert.Equal("saga persistence failed", exception.Message);
        Assert.Same(inner, exception.InnerException);
    }

    [Fact]
    public async Task SagaMapper_ResolvesStartingAndContinuingCorrelation()
    {
        var mapper = new SagaMapper<OrderSagaData>()
            .StartsWith<StartOrder>(message => message.OrderId)
            .Correlate<OrderBilled>(message => message.OrderId);

        Assert.True(mapper.TryGetRule(typeof(StartOrder), out var startRule));
        Assert.True(startRule.StartsSaga);
        Assert.Equal("order-1", await startRule.ResolveCorrelationIdAsync(
            new StartOrder("order-1"),
            new RecordingMiniBusContext(),
            CancellationToken.None));

        Assert.True(mapper.TryGetRule(typeof(OrderBilled), out var continueRule));
        Assert.False(continueRule.StartsSaga);
        Assert.Equal("order-1", await continueRule.ResolveCorrelationIdAsync(
            new OrderBilled("order-1"),
            new RecordingMiniBusContext(),
            CancellationToken.None));
    }

    [Fact]
    public async Task SagaMapper_UsesCustomFinder()
    {
        var mapper = new SagaMapper<OrderSagaData>()
            .FindWith(new OrderFinder(), startsSaga: true);

        Assert.True(mapper.TryGetRule(typeof(StartOrder), out var rule));
        Assert.Equal("found-order-1", await rule.ResolveCorrelationIdAsync(
            new StartOrder("order-1"),
            new RecordingMiniBusContext(),
            CancellationToken.None));
    }

    [Fact]
    public void SagaMapper_ThrowsForDuplicateCorrelationMapping()
    {
        var mapper = new SagaMapper<OrderSagaData>()
            .StartsWith<StartOrder>(message => message.OrderId);

        var exception = Assert.Throws<SagaMappingException>(() =>
            mapper.Correlate<StartOrder>(message => message.OrderId));

        Assert.Contains(typeof(StartOrder).FullName!, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SagaRegistry_ThrowsWhenSagaHasNoMappings()
    {
        var registry = new SagaRegistry();

        var exception = Assert.Throws<SagaMappingException>(
            () => registry.Register<UnmappedSaga, OrderSagaData>());

        Assert.Contains("must configure at least one", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SagaRegistry_DefinitionsEnumerationRemainsStableDuringRegistration()
    {
        var registry = new SagaRegistry();
        registry.Register<OrderSaga, OrderSagaData>();

        var firstItemRead = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var continueEnumeration = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);

        var reader = Task.Run(async () =>
        {
            var sagaTypes = new List<Type>();

            foreach (var definition in registry.Definitions)
            {
                sagaTypes.Add(definition.SagaType);
                firstItemRead.TrySetResult(null);
                await continueEnumeration.Task;
            }

            return sagaTypes;
        });

        await firstItemRead.Task;
        registry.Register<TimeoutStartingSaga, OrderSagaData>();
        continueEnumeration.SetResult(null);

        var observedSagaTypes = await reader;

        Assert.Equal([typeof(OrderSaga)], observedSagaTypes);
        Assert.Equal(2, registry.Definitions.Count);
    }

    [Fact]
    public void SagaRegistry_DoesNotExposeMutableBackingCollections()
    {
        var registry = new SagaRegistry();
        registry.Register<OrderSaga, OrderSagaData>();

        Assert.False(registry.Definitions is SagaDefinition[]);
        Assert.False(registry.GetDefinitionsForMessage(typeof(StartOrder)) is SagaDefinition[]);
    }

    [Fact]
    public async Task InMemorySagaPersistence_LoadCreateSaveAndComplete()
    {
        var persistence = new InMemorySagaPersistence();
        var data = new OrderSagaData { Id = Guid.NewGuid(), CorrelationId = "order-1" };

        await persistence.CreateAsync(data);
        var loaded = await persistence.LoadAsync<OrderSagaData>("order-1");
        Assert.NotNull(loaded);
        Assert.NotSame(data, loaded.Data);
        Assert.Equal(data.Id, loaded.Data.Id);
        Assert.Equal("1", loaded.Version);

        data.Step = "saved";
        await persistence.SaveAsync(data, loaded.Version);
        var saved = await persistence.LoadAsync<OrderSagaData>("order-1");
        Assert.NotNull(saved);
        Assert.Equal("saved", saved.Data.Step);
        Assert.Equal("2", saved.Version);

        await persistence.CompleteAsync(saved.Data, saved.Version);
        var completed = await persistence.LoadAsync<OrderSagaData>("order-1");
        Assert.NotNull(completed);
        Assert.True(completed.Data.IsCompleted);
        Assert.Equal("3", completed.Version);
    }

    [Fact]
    public async Task InMemorySagaPersistence_RejectsCompletionRegression()
    {
        var persistence = new InMemorySagaPersistence();
        var data = new OrderSagaData { Id = Guid.NewGuid(), CorrelationId = "order-1" };

        await persistence.CreateAsync(data);
        var loaded = await persistence.LoadAsync<OrderSagaData>("order-1");
        Assert.NotNull(loaded);

        await persistence.CompleteAsync(loaded.Data, loaded.Version);

        var completed = await persistence.LoadAsync<OrderSagaData>("order-1");
        Assert.NotNull(completed);
        completed.Data.IsCompleted = false;

        var exception = await Assert.ThrowsAsync<SagaPersistenceException>(() =>
            persistence.SaveAsync(completed.Data, completed.Version));

        Assert.Contains("cannot be marked incomplete", exception.Message, StringComparison.Ordinal);

        var reloaded = await persistence.LoadAsync<OrderSagaData>("order-1");
        Assert.NotNull(reloaded);
        Assert.True(reloaded.Data.IsCompleted);
        Assert.Equal(completed.Version, reloaded.Version);
    }

    [Fact]
    public async Task InMemorySagaPersistence_DeepClonesReferenceTypeProperties()
    {
        var persistence = new InMemorySagaPersistence();
        var data = new OrderSagaData
        {
            Id = Guid.NewGuid(),
            CorrelationId = "order-1",
            Events = ["created"]
        };

        await persistence.CreateAsync(data);
        var loaded = await persistence.LoadAsync<OrderSagaData>("order-1");
        Assert.NotNull(loaded);

        loaded.Data.Events.Add("mutated-without-save");

        var reloaded = await persistence.LoadAsync<OrderSagaData>("order-1");
        Assert.NotNull(reloaded);
        Assert.Equal(["created"], reloaded.Data.Events);
    }

    [Fact]
    public async Task SagaInvoker_StartsNewSagaAndPersistsState()
    {
        ResetSagaCounters();
        var persistence = new InMemorySagaPersistence();
        var invoker = CreateInvoker(persistence);
        var context = new RecordingMiniBusContext();

        await invoker.InvokeAsync(
            new StartOrder("order-1"),
            context,
            EmptyServiceProvider.Instance,
            CancellationToken.None);

        var stored = await persistence.LoadAsync<OrderSagaData>("order-1");
        Assert.NotNull(stored);
        Assert.Equal("started", stored.Data.Step);
        Assert.Equal(1, OrderSaga.StartedCount);
        Assert.Same(context, OrderSaga.LastContext);
    }

    [Fact]
    public async Task SagaInvoker_StartsAndCompletesNewSagaThroughCreate()
    {
        var persistence = new InMemorySagaPersistence();
        var registry = new SagaRegistry();
        registry.Register<CompletingStartingSaga, OrderSagaData>();
        var invoker = new SagaInvoker(registry, persistence);

        await invoker.InvokeAsync(
            new StartAndCompleteOrder("order-1"),
            new RecordingMiniBusContext(),
            EmptyServiceProvider.Instance,
            CancellationToken.None);

        var stored = await persistence.LoadAsync<OrderSagaData>("order-1");
        Assert.NotNull(stored);
        Assert.True(stored.Data.IsCompleted);
        Assert.Equal("completed", stored.Data.Step);
        Assert.Equal("1", stored.Version);
    }

    [Fact]
    public async Task SagaInvoker_LoadsExistingSagaAndSavesState()
    {
        ResetSagaCounters();
        var persistence = new InMemorySagaPersistence();
        await persistence.CreateAsync(new OrderSagaData
        {
            Id = Guid.NewGuid(),
            CorrelationId = "order-1",
            Step = "started"
        });
        var loadedBefore = await persistence.LoadAsync<OrderSagaData>("order-1");
        var invoker = CreateInvoker(persistence);

        await invoker.InvokeAsync(
            new OrderBilled("order-1"),
            new RecordingMiniBusContext(),
            EmptyServiceProvider.Instance,
            CancellationToken.None);

        var stored = await persistence.LoadAsync<OrderSagaData>("order-1");
        Assert.NotNull(stored);
        Assert.Equal("billed", stored.Data.Step);
        Assert.Equal("2", stored.Version);
        Assert.Equal("1", loadedBefore!.Version);
        Assert.Equal(1, OrderSaga.BilledCount);
    }

    [Fact]
    public async Task SagaInvoker_ThrowsClearErrorWhenLoadedSagaHasNoVersion()
    {
        var invoker = CreateInvoker(new MissingVersionSagaPersistence());

        var exception = await Assert.ThrowsAsync<SagaPersistenceException>(() => invoker.InvokeAsync(
            new OrderBilled("order-1"),
            new RecordingMiniBusContext(),
            EmptyServiceProvider.Instance,
            CancellationToken.None));

        Assert.Contains("without a version token", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SagaInvoker_DoesNotSaveStateWhenHandlerFails()
    {
        ResetSagaCounters();
        var persistence = new InMemorySagaPersistence();
        await persistence.CreateAsync(new OrderSagaData
        {
            Id = Guid.NewGuid(),
            CorrelationId = "order-1",
            Step = "started"
        });
        var invoker = CreateInvoker(persistence);

        await Assert.ThrowsAsync<InvalidOperationException>(() => invoker.InvokeAsync(
            new FailOrder("order-1"),
            new RecordingMiniBusContext(),
            EmptyServiceProvider.Instance,
            CancellationToken.None));

        var stored = await persistence.LoadAsync<OrderSagaData>("order-1");
        Assert.NotNull(stored);
        Assert.Equal("started", stored.Data.Step);
        Assert.Equal("1", stored.Version);
    }

    [Fact]
    public async Task SagaInvoker_DoesNotInvokeCompletedSaga()
    {
        ResetSagaCounters();
        var persistence = new InMemorySagaPersistence();
        await persistence.CreateAsync(new OrderSagaData
        {
            Id = Guid.NewGuid(),
            CorrelationId = "order-1",
            IsCompleted = true
        });
        var invoker = CreateInvoker(persistence);

        await invoker.InvokeAsync(
            new OrderBilled("order-1"),
            new RecordingMiniBusContext(),
            EmptyServiceProvider.Instance,
            CancellationToken.None);

        Assert.Equal(0, OrderSaga.BilledCount);
    }

    [Fact]
    public async Task SagaInvoker_CompletesSagaState()
    {
        ResetSagaCounters();
        var persistence = new InMemorySagaPersistence();
        await persistence.CreateAsync(new OrderSagaData
        {
            Id = Guid.NewGuid(),
            CorrelationId = "order-1"
        });
        var invoker = CreateInvoker(persistence);

        await invoker.InvokeAsync(
            new CompleteOrder("order-1"),
            new RecordingMiniBusContext(),
            EmptyServiceProvider.Instance,
            CancellationToken.None);

        var stored = await persistence.LoadAsync<OrderSagaData>("order-1");
        Assert.NotNull(stored);
        Assert.True(stored.Data.IsCompleted);
    }

    [Fact]
    public async Task Saga_RequestTimeoutWithAbsoluteDueTimeSchedulesTimeoutMessage()
    {
        var saga = new OrderSaga();
        var context = new RecordingMiniBusContext();
        var dueTime = DateTimeOffset.UtcNow.AddMinutes(5);

        await saga.RequestTimeoutForTest(new OrderTimeout("order-1"), dueTime, context);

        var schedule = Assert.Single(context.Schedules);
        Assert.Equal(new OrderTimeout("order-1"), schedule.Message);
        Assert.Equal(dueTime, schedule.DueTime);
    }

    [Fact]
    public async Task Saga_RequestTimeoutWithDelaySchedulesTimeoutMessageInFuture()
    {
        var saga = new OrderSaga();
        var context = new RecordingMiniBusContext();
        var before = DateTimeOffset.UtcNow.AddMinutes(5);

        await saga.RequestTimeoutForTest(new OrderTimeout("order-1"), TimeSpan.FromMinutes(5), context);

        var after = DateTimeOffset.UtcNow.AddMinutes(5);
        var schedule = Assert.Single(context.Schedules);
        Assert.Equal(new OrderTimeout("order-1"), schedule.Message);
        Assert.InRange(schedule.DueTime, before, after);
    }

    [Fact]
    public async Task Saga_RequestTimeoutValidatesInputs()
    {
        var saga = new OrderSaga();
        var context = new RecordingMiniBusContext();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            saga.RequestTimeoutForTest(null!, DateTimeOffset.UtcNow, context));
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            saga.RequestTimeoutForTest(new OrderTimeout("order-1"), DateTimeOffset.UtcNow, null!));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            saga.RequestTimeoutForTest(new OrderTimeout("order-1"), TimeSpan.FromTicks(-1), context));

        Assert.Empty(context.Schedules);
    }

    [Fact]
    public async Task SagaInvoker_TimeoutMessageUsesContinuingCorrelation()
    {
        ResetSagaCounters();
        var persistence = new InMemorySagaPersistence();
        await persistence.CreateAsync(new OrderSagaData
        {
            Id = Guid.NewGuid(),
            CorrelationId = "order-1",
            Step = "started"
        });
        var invoker = CreateInvoker(persistence);

        await invoker.InvokeAsync(
            new OrderTimeout("order-1"),
            new RecordingMiniBusContext(),
            EmptyServiceProvider.Instance,
            CancellationToken.None);

        var stored = await persistence.LoadAsync<OrderSagaData>("order-1");
        Assert.NotNull(stored);
        Assert.Equal("timed-out", stored.Data.Step);
        Assert.Equal(1, OrderSaga.TimeoutCount);
    }

    [Fact]
    public async Task SagaInvoker_TimeoutMessageDoesNotCreateSagaWhenMappedAsContinuing()
    {
        ResetSagaCounters();
        var persistence = new InMemorySagaPersistence();
        var invoker = CreateInvoker(persistence);

        await invoker.InvokeAsync(
            new OrderTimeout("missing-order"),
            new RecordingMiniBusContext(),
            EmptyServiceProvider.Instance,
            CancellationToken.None);

        var stored = await persistence.LoadAsync<OrderSagaData>("missing-order");
        Assert.Null(stored);
        Assert.Equal(0, OrderSaga.TimeoutCount);
    }

    [Fact]
    public async Task SagaInvoker_TimeoutMessageCanStartSagaWhenExplicitlyMappedAsStartingMessage()
    {
        var persistence = new InMemorySagaPersistence();
        var registry = new SagaRegistry();
        registry.Register<TimeoutStartingSaga, OrderSagaData>();
        var invoker = new SagaInvoker(registry, persistence);

        await invoker.InvokeAsync(
            new OrderTimeout("order-1"),
            new RecordingMiniBusContext(),
            EmptyServiceProvider.Instance,
            CancellationToken.None);

        var stored = await persistence.LoadAsync<OrderSagaData>("order-1");
        Assert.NotNull(stored);
        Assert.Equal("started-by-timeout", stored.Data.Step);
    }

    [Fact]
    public async Task SagaInvoker_DoesNotInvokeTimeoutForCompletedSaga()
    {
        ResetSagaCounters();
        var persistence = new InMemorySagaPersistence();
        await persistence.CreateAsync(new OrderSagaData
        {
            Id = Guid.NewGuid(),
            CorrelationId = "order-1",
            IsCompleted = true
        });
        var invoker = CreateInvoker(persistence);

        await invoker.InvokeAsync(
            new OrderTimeout("order-1"),
            new RecordingMiniBusContext(),
            EmptyServiceProvider.Instance,
            CancellationToken.None);

        Assert.Equal(0, OrderSaga.TimeoutCount);
    }

    [Fact]
    public async Task SagaInvoker_DoesNotSaveStateWhenTimeoutHandlerFails()
    {
        ResetSagaCounters();
        var persistence = new InMemorySagaPersistence();
        await persistence.CreateAsync(new OrderSagaData
        {
            Id = Guid.NewGuid(),
            CorrelationId = "order-1",
            Step = "started"
        });
        var invoker = CreateInvoker(persistence);

        await Assert.ThrowsAsync<InvalidOperationException>(() => invoker.InvokeAsync(
            new FailingOrderTimeout("order-1"),
            new RecordingMiniBusContext(),
            EmptyServiceProvider.Instance,
            CancellationToken.None));

        var stored = await persistence.LoadAsync<OrderSagaData>("order-1");
        Assert.NotNull(stored);
        Assert.Equal("started", stored.Data.Step);
    }

    private static SagaInvoker CreateInvoker(ISagaPersistence persistence)
    {
        var registry = new SagaRegistry();
        registry.Register<OrderSaga, OrderSagaData>();

        return new SagaInvoker(registry, persistence);
    }

    private static void ResetSagaCounters()
    {
        OrderSaga.StartedCount = 0;
        OrderSaga.BilledCount = 0;
        OrderSaga.TimeoutCount = 0;
        OrderSaga.LastContext = null;
    }

    private sealed record StartOrder(string OrderId) : ICommand;

    private sealed record StartAndCompleteOrder(string OrderId) : ICommand;

    private sealed record OrderBilled(string OrderId) : IEvent;

    private sealed record FailOrder(string OrderId) : IEvent;

    private sealed record CompleteOrder(string OrderId) : IEvent;

    private sealed record OrderTimeout(string OrderId) : ISagaTimeout;

    private sealed record FailingOrderTimeout(string OrderId) : ISagaTimeout;

    private sealed class OrderSagaData : ISagaData
    {
        public Guid Id { get; set; }

        public string CorrelationId { get; set; } = string.Empty;

        public bool IsCompleted { get; set; }

        public string? Step { get; set; }

        public List<string> Events { get; set; } = new();
    }

    private sealed class OrderSaga :
        MiniBusSaga<OrderSagaData>,
        IHandleSagaMessages<StartOrder>,
        IHandleSagaMessages<OrderBilled>,
        IHandleSagaMessages<FailOrder>,
        IHandleSagaMessages<CompleteOrder>,
        IHandleSagaMessages<OrderTimeout>,
        IHandleSagaMessages<FailingOrderTimeout>
    {
        public static int StartedCount { get; set; }

        public static int BilledCount { get; set; }

        public static int TimeoutCount { get; set; }

        public static MiniBusContext? LastContext { get; set; }

        public override void ConfigureHowToFindSaga(SagaMapper<OrderSagaData> mapper)
        {
            mapper.StartsWith<StartOrder>(message => message.OrderId)
                .Correlate<OrderBilled>(message => message.OrderId)
                .Correlate<FailOrder>(message => message.OrderId)
                .Correlate<CompleteOrder>(message => message.OrderId)
                .Correlate<OrderTimeout>(message => message.OrderId)
                .Correlate<FailingOrderTimeout>(message => message.OrderId);
        }

        public Task Handle(StartOrder message, MiniBusContext context, CancellationToken cancellationToken)
        {
            StartedCount++;
            LastContext = context;
            Data.Step = "started";
            return Task.CompletedTask;
        }

        public Task Handle(OrderBilled message, MiniBusContext context, CancellationToken cancellationToken)
        {
            BilledCount++;
            Data.Step = "billed";
            return Task.CompletedTask;
        }

        public Task Handle(FailOrder message, MiniBusContext context, CancellationToken cancellationToken)
        {
            Data.Step = "failed";
            return Task.FromException(new InvalidOperationException("saga failed"));
        }

        public Task Handle(CompleteOrder message, MiniBusContext context, CancellationToken cancellationToken)
        {
            MarkAsComplete();
            return Task.CompletedTask;
        }

        public Task Handle(OrderTimeout message, MiniBusContext context, CancellationToken cancellationToken)
        {
            TimeoutCount++;
            Data.Step = "timed-out";
            return Task.CompletedTask;
        }

        public Task Handle(FailingOrderTimeout message, MiniBusContext context, CancellationToken cancellationToken)
        {
            Data.Step = "timeout-failed";
            throw new InvalidOperationException("timeout failed");
        }

        public void AttachForTest(OrderSagaData data)
        {
            AttachData(data);
        }

        public Task RequestTimeoutForTest(
            OrderTimeout timeout,
            DateTimeOffset dueTime,
            MiniBusContext context)
        {
            return RequestTimeout(timeout, dueTime, context);
        }

        public Task RequestTimeoutForTest(
            OrderTimeout timeout,
            TimeSpan delay,
            MiniBusContext context)
        {
            return RequestTimeout(timeout, delay, context);
        }
    }

    private sealed class TimeoutStartingSaga :
        MiniBusSaga<OrderSagaData>,
        IHandleSagaMessages<OrderTimeout>
    {
        public override void ConfigureHowToFindSaga(SagaMapper<OrderSagaData> mapper)
        {
            mapper.StartsWith<OrderTimeout>(message => message.OrderId);
        }

        public Task Handle(OrderTimeout message, MiniBusContext context, CancellationToken cancellationToken)
        {
            Data.Step = "started-by-timeout";
            return Task.CompletedTask;
        }
    }

    private sealed class CompletingStartingSaga :
        MiniBusSaga<OrderSagaData>,
        IHandleSagaMessages<StartAndCompleteOrder>
    {
        public override void ConfigureHowToFindSaga(SagaMapper<OrderSagaData> mapper)
        {
            mapper.StartsWith<StartAndCompleteOrder>(message => message.OrderId);
        }

        public Task Handle(StartAndCompleteOrder message, MiniBusContext context, CancellationToken cancellationToken)
        {
            Data.Step = "completed";
            MarkAsComplete();
            return Task.CompletedTask;
        }
    }

    private sealed class MissingVersionSagaPersistence : ISagaPersistence
    {
        public Task<SagaPersistenceRecord<TData>?> LoadAsync<TData>(
            string correlationId,
            CancellationToken cancellationToken = default)
            where TData : class, ISagaData, new()
        {
            var data = new TData
            {
                Id = Guid.NewGuid(),
                CorrelationId = correlationId
            };
            return Task.FromResult<SagaPersistenceRecord<TData>?>(
                new SagaPersistenceRecord<TData>(data, Version: null!));
        }

        public Task CreateAsync<TData>(
            TData data,
            CancellationToken cancellationToken = default)
            where TData : class, ISagaData, new()
        {
            throw new NotSupportedException();
        }

        public Task SaveAsync<TData>(
            TData data,
            string version,
            CancellationToken cancellationToken = default)
            where TData : class, ISagaData, new()
        {
            throw new NotSupportedException();
        }

        public Task CompleteAsync<TData>(
            TData data,
            string version,
            CancellationToken cancellationToken = default)
            where TData : class, ISagaData, new()
        {
            throw new NotSupportedException();
        }
    }

    private sealed class UnmappedSaga : MiniBusSaga<OrderSagaData>
    {
        public override void ConfigureHowToFindSaga(SagaMapper<OrderSagaData> mapper)
        {
        }
    }

    private sealed class OrderFinder : ISagaFinder<StartOrder, OrderSagaData>
    {
        public Task<string?> FindCorrelationId(StartOrder message, MiniBusContext context, CancellationToken cancellationToken)
        {
            return Task.FromResult<string?>($"found-{message.OrderId}");
        }
    }

    private sealed class RecordingMiniBusContext : MiniBusContext
    {
        public List<(object Message, DateTimeOffset DueTime)> Schedules { get; } = new();

        public override string EndpointName => "Tests";

        public override string MessageId => "message-id";

        public override string CorrelationId => "correlation-id";

        public override string? CausationId => "causation-id";

        public override IReadOnlyDictionary<string, string> Headers { get; } = new Dictionary<string, string>
        {
            ["Custom"] = "custom-value"
        };

        public override Task Send<TCommand>(TCommand command, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public override Task Publish<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public override Task Schedule<TMessage>(TMessage message, DateTimeOffset dueTime, CancellationToken cancellationToken = default)
        {
            Schedules.Add((message!, dueTime));
            return Task.CompletedTask;
        }
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public static EmptyServiceProvider Instance { get; } = new();

        public object? GetService(Type serviceType)
        {
            return null;
        }
    }
}
