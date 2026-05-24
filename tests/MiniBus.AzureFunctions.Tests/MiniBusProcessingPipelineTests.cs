using Azure.Messaging.ServiceBus;
using MiniBus.AzureFunctions.Processing;
using MiniBus.AzureFunctions.Processing.Pipeline;
using MiniBus.AzureServiceBus.TransportMessageMapping;
using MiniBus.Core.Contracts;
using MiniBus.Core.Serialization;

namespace MiniBus.AzureFunctions.Tests;

public sealed class MiniBusProcessingPipelineTests
{
    [Fact]
    public async Task Pipeline_ExecutesBehaviorsInOrderAndSharesContextState()
    {
        var order = new List<string>();
        var pipeline = new MiniBusProcessingPipeline(new IMiniBusProcessingBehavior[]
        {
            new RecordingBehavior("first", order, context => context.Headers = new Dictionary<string, string>
            {
                ["value"] = "from-first"
            }),
            new RecordingBehavior("second", order, context => context.MessageType = typeof(TestCommand)),
            new RecordingBehavior("third", order, context =>
            {
                Assert.Equal("from-first", context.Headers["value"]);
                Assert.Equal(typeof(TestCommand), context.MessageType);
            })
        });
        var context = new MiniBusProcessingContext(CreateMessage(), new MiniBusProcessorOptions());

        await pipeline.InvokeAsync(context);

        Assert.Equal(new[] { "first-before", "second-before", "third-before", "third-after", "second-after", "first-after" }, order);
    }

    [Fact]
    public async Task Pipeline_StopsRemainingBehaviorsWhenContextIsShortCircuited()
    {
        var order = new List<string>();
        var pipeline = new MiniBusProcessingPipeline(new IMiniBusProcessingBehavior[]
        {
            new RecordingBehavior("first", order, context => context.ShortCircuit()),
            new RecordingBehavior("second", order)
        });
        var context = new MiniBusProcessingContext(CreateMessage(), new MiniBusProcessorOptions());

        await pipeline.InvokeAsync(context);

        Assert.True(context.IsShortCircuited);
        Assert.Equal(new[] { "first-before", "first-after" }, order);
    }

    [Fact]
    public async Task MetadataTypeAndDeserializationBehaviorsPopulatePipelineContext()
    {
        var serializer = new RecordingSerializer(new TestCommand(Guid.NewGuid()));
        var pipeline = new MiniBusProcessingPipeline(new IMiniBusProcessingBehavior[]
        {
            new ReceivedMessageHeadersBehavior(),
            new MessageTypeResolutionBehavior(),
            new MessageDeserializationBehavior(serializer)
        });
        var message = CreateMessage(new Dictionary<string, object>
        {
            [MiniBusHeaderNames.MessageType] = typeof(TestCommand).AssemblyQualifiedName!,
            [MiniBusHeaderNames.MessageId] = "message-1"
        });
        var context = new MiniBusProcessingContext(message, new MiniBusProcessorOptions());

        await pipeline.InvokeAsync(context);

        Assert.Equal("message-1", context.Headers[MiniBusHeaderNames.MessageId]);
        Assert.Equal(typeof(TestCommand), context.MessageType);
        Assert.IsType<TestCommand>(context.DeserializedMessage);
        Assert.Equal(typeof(TestCommand), serializer.DeserializedType);
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

    private sealed class RecordingBehavior : IMiniBusProcessingBehavior
    {
        private readonly string _name;
        private readonly List<string> _order;
        private readonly Action<MiniBusProcessingContext>? _beforeNext;

        public RecordingBehavior(
            string name,
            List<string> order,
            Action<MiniBusProcessingContext>? beforeNext = null)
        {
            _name = name;
            _order = order;
            _beforeNext = beforeNext;
        }

        public async Task InvokeAsync(
            MiniBusProcessingContext context,
            MiniBusProcessingDelegate next,
            CancellationToken cancellationToken)
        {
            _order.Add($"{_name}-before");
            _beforeNext?.Invoke(context);
            await next(context, cancellationToken);
            _order.Add($"{_name}-after");
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
            throw new NotSupportedException();
        }

        public object Deserialize(BinaryData body, Type messageType)
        {
            DeserializedType = messageType;
            return _message;
        }
    }
}
