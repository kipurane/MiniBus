using MiniBus.Core.Contracts;

namespace MiniBus.Testing.Tests;

public sealed class TestableMiniBusContextTests
{
    [Fact]
    public void Constructor_UsesDeterministicDefaultMetadata()
    {
        var context = new TestableMiniBusContext();

        Assert.Equal("Tests", context.EndpointName);
        Assert.Equal("message-id", context.MessageId);
        Assert.Equal("correlation-id", context.CorrelationId);
        Assert.Null(context.CausationId);
        Assert.Empty(context.Headers);
    }

    [Fact]
    public void Constructor_UsesConfiguredMetadataAndHeaders()
    {
        var context = new TestableMiniBusContext(
            endpointName: "Billing",
            messageId: "message-1",
            correlationId: "correlation-1",
            causationId: "causation-1",
            headers: new Dictionary<string, string>
            {
                ["Custom"] = "custom-value"
            });

        Assert.Equal("Billing", context.EndpointName);
        Assert.Equal("message-1", context.MessageId);
        Assert.Equal("correlation-1", context.CorrelationId);
        Assert.Equal("causation-1", context.CausationId);
        Assert.Equal("custom-value", context.Headers["Custom"]);

        context.HeaderValues["Another"] = "another-value";

        Assert.Equal("another-value", context.Headers["Another"]);
    }

    [Fact]
    public async Task Send_CapturesOriginalCommandAndType()
    {
        var context = new TestableMiniBusContext();
        var command = new TestCommand("command-1");

        await context.Send(command);

        var operation = Assert.Single(context.SentMessages);
        Assert.Same(command, operation.Message);
        Assert.Equal(typeof(TestCommand), operation.MessageType);
        Assert.Same(command, operation.GetMessage<TestCommand>());
    }

    [Fact]
    public async Task Publish_CapturesOriginalEventAndType()
    {
        var context = new TestableMiniBusContext();
        var @event = new TestEvent("event-1");

        await context.Publish(@event);

        var operation = Assert.Single(context.PublishedMessages);
        Assert.Same(@event, operation.Message);
        Assert.Equal(typeof(TestEvent), operation.MessageType);
        Assert.Same(@event, operation.GetMessage<TestEvent>());
    }

    [Fact]
    public async Task Schedule_CapturesOriginalMessageTypeAndDueTime()
    {
        var context = new TestableMiniBusContext();
        var message = new TestMessage("message-1");
        var dueTime = DateTimeOffset.UtcNow.AddMinutes(5);

        await context.Schedule(message, dueTime);

        var operation = Assert.Single(context.ScheduledMessages);
        Assert.Same(message, operation.Message);
        Assert.Equal(typeof(TestMessage), operation.MessageType);
        Assert.Equal(dueTime, operation.DueTime);
        Assert.Same(message, operation.GetMessage<TestMessage>());
    }

    [Fact]
    public async Task TypedHelpers_ReturnMatchingOperations()
    {
        var context = new TestableMiniBusContext();
        var command = new TestCommand("command-1");
        var otherCommand = new OtherCommand("command-2");
        var @event = new TestEvent("event-1");
        var message = new TestMessage("message-1");
        var dueTime = DateTimeOffset.UtcNow.AddMinutes(1);

        await context.Send(command);
        await context.Send(otherCommand);
        await context.Publish(@event);
        await context.Schedule(message, dueTime);

        var sent = Assert.Single(context.Sent<TestCommand>());
        var published = Assert.Single(context.Published<TestEvent>());
        var scheduled = Assert.Single(context.Scheduled<TestMessage>());

        Assert.Same(command, sent.Message);
        Assert.Equal(typeof(TestCommand), sent.MessageType);
        Assert.Same(@event, published.Message);
        Assert.Equal(typeof(TestEvent), published.MessageType);
        Assert.Same(message, scheduled.Message);
        Assert.Equal(typeof(TestMessage), scheduled.MessageType);
        Assert.Equal(dueTime, scheduled.DueTime);
    }

    [Fact]
    public async Task SingleHelpers_ReturnOnlyMatchingOperation()
    {
        var context = new TestableMiniBusContext();
        var command = new TestCommand("command-1");
        var @event = new TestEvent("event-1");
        var message = new TestMessage("message-1");
        var dueTime = DateTimeOffset.UtcNow.AddMinutes(1);

        await context.Send(command);
        await context.Publish(@event);
        await context.Schedule(message, dueTime);

        Assert.Same(command, context.SingleSent<TestCommand>().Message);
        Assert.Same(@event, context.SinglePublished<TestEvent>().Message);
        Assert.Same(message, context.SingleScheduled<TestMessage>().Message);
    }

    [Fact]
    public async Task SingleHelpers_FailClearlyWhenZeroOrMultipleOperationsMatch()
    {
        var context = new TestableMiniBusContext();

        var missing = Assert.Throws<InvalidOperationException>(() => context.SingleSent<TestCommand>());
        Assert.Contains("none were captured", missing.Message, StringComparison.Ordinal);

        await context.Send(new TestCommand("command-1"));
        await context.Send(new TestCommand("command-2"));

        var multiple = Assert.Throws<InvalidOperationException>(() => context.SingleSent<TestCommand>());
        Assert.Contains("2 were captured", multiple.Message, StringComparison.Ordinal);
    }

    private sealed record TestMessage(string Id) : IMessage;

    private sealed record TestCommand(string Id) : ICommand;

    private sealed record OtherCommand(string Id) : ICommand;

    private sealed record TestEvent(string Id) : IEvent;
}
