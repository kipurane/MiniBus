using MiniBus.Core.Contracts;
using MiniBus.Core.Persistence;
using Xunit;

namespace MiniBus.Core.Tests;

public sealed class OutboxOperationCollectorTests
{
    [Fact]
    public void OutboxOperationCollector_StartsEmpty()
    {
        var collector = new MiniBusOutboxOperationCollector();

        Assert.Empty(collector.Operations);
    }

    [Fact]
    public void OutboxOperationCollector_TracksAddedOperations()
    {
        var collector = new MiniBusOutboxOperationCollector();
        var headers = new Dictionary<string, string>(StringComparer.Ordinal);
        var operation1 = new MiniBusOutboxOperation(
            MiniBusOutboxOperationKind.Send,
            new TestCommand(Guid.NewGuid()),
            typeof(TestCommand),
            headers,
            DueTime: null);
        var operation2 = new MiniBusOutboxOperation(
            MiniBusOutboxOperationKind.Publish,
            new TestEvent(Guid.NewGuid()),
            typeof(TestEvent),
            headers,
            DueTime: null);

        collector.Add(operation1);
        collector.Add(operation2);

        Assert.Equal(2, collector.Operations.Count);
        Assert.Same(operation1, collector.Operations[0]);
        Assert.Same(operation2, collector.Operations[1]);
    }

    [Fact]
    public void OutboxOperationCollector_TracksScheduledOperationWithDueTime()
    {
        var collector = new MiniBusOutboxOperationCollector();
        var dueTime = DateTimeOffset.UtcNow.AddMinutes(5);
        var headers = new Dictionary<string, string>(StringComparer.Ordinal);
        var operation = new MiniBusOutboxOperation(
            MiniBusOutboxOperationKind.Schedule,
            new TestCommand(Guid.NewGuid()),
            typeof(TestCommand),
            headers,
            DueTime: dueTime);

        collector.Add(operation);

        var added = Assert.Single(collector.Operations);
        Assert.Equal(MiniBusOutboxOperationKind.Schedule, added.Kind);
        Assert.Equal(dueTime, added.DueTime);
    }

    [Fact]
    public void OutboxOperationCollector_ThrowsOnNullOperation()
    {
        var collector = new MiniBusOutboxOperationCollector();

        Assert.Throws<ArgumentNullException>(() => collector.Add(null!));
    }

    private sealed record TestCommand(Guid Id) : ICommand;

    private sealed record TestEvent(Guid Id) : IEvent;
}
