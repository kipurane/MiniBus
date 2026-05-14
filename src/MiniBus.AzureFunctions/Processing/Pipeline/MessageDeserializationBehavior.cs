using MiniBus.Core.Serialization;

namespace MiniBus.AzureFunctions.Processing.Pipeline;

internal sealed class MessageDeserializationBehavior : IMiniBusProcessingBehavior
{
    private readonly IMessageSerializer _serializer;

    public MessageDeserializationBehavior(IMessageSerializer serializer)
    {
        _serializer = serializer;
    }

    public Task InvokeAsync(
        MiniBusProcessingContext context,
        MiniBusProcessingDelegate next,
        CancellationToken cancellationToken)
    {
        if (context.MessageType is null)
        {
            throw new InvalidOperationException("MiniBus message type must be resolved before deserialization.");
        }

        context.DeserializedMessage = _serializer.Deserialize(context.Message.Body, context.MessageType);
        return next(context, cancellationToken);
    }
}
