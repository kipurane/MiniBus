using MiniBus.Core.Context;
using MiniBus.Core.Contracts;

namespace MiniBus.Core.Sagas;

public class SagaCorrelationRule
{
    private readonly Func<object, MiniBusContext, CancellationToken, Task<string?>> _resolveCorrelationId;

    internal SagaCorrelationRule(
        Type messageType,
        bool startsSaga,
        Func<object, MiniBusContext, CancellationToken, Task<string?>> resolveCorrelationId)
    {
        MessageType = messageType;
        StartsSaga = startsSaga;
        _resolveCorrelationId = resolveCorrelationId;
    }

    public Type MessageType { get; }

    public bool StartsSaga { get; }

    public Task<string?> ResolveCorrelationIdAsync(
        object message,
        MiniBusContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(context);

        if (!MessageType.IsInstanceOfType(message))
        {
            throw new SagaMappingException($"Message '{message.GetType().FullName}' is not assignable to saga correlation message type '{MessageType.FullName}'.");
        }

        return _resolveCorrelationId(message, context, cancellationToken);
    }
}

public sealed class SagaCorrelationRule<TMessage> : SagaCorrelationRule
    where TMessage : IMessage
{
    internal SagaCorrelationRule(
        bool startsSaga,
        Func<TMessage, MiniBusContext, CancellationToken, Task<string?>> resolveCorrelationId)
        : base(
            typeof(TMessage),
            startsSaga,
            (message, context, cancellationToken) => resolveCorrelationId((TMessage)message, context, cancellationToken))
    {
    }
}
