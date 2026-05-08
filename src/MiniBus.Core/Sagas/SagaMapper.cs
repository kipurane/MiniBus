using System.Linq.Expressions;
using MiniBus.Core.Context;
using MiniBus.Core.Contracts;

namespace MiniBus.Core.Sagas;

public sealed class SagaMapper<TData>
    where TData : class, ISagaData, new()
{
    private readonly Dictionary<Type, SagaCorrelationRule> _rules = new();

    public IReadOnlyCollection<SagaCorrelationRule> Rules => _rules.Values;

    public SagaMapper<TData> StartsWith<TMessage>(
        Func<TMessage, string?> correlationId)
        where TMessage : IMessage
    {
        ArgumentNullException.ThrowIfNull(correlationId);

        return AddRule<TMessage>(
            startsSaga: true,
            (message, _, _) => Task.FromResult(correlationId(message)));
    }

    public SagaMapper<TData> Correlate<TMessage>(
        Func<TMessage, string?> correlationId)
        where TMessage : IMessage
    {
        ArgumentNullException.ThrowIfNull(correlationId);

        return AddRule<TMessage>(
            startsSaga: false,
            (message, _, _) => Task.FromResult(correlationId(message)));
    }

    public SagaMapper<TData> StartsWith<TMessage>(
        Expression<Func<TMessage, object?>> messageProperty)
        where TMessage : IMessage
    {
        return StartsWith(CompilePropertyAccessor(messageProperty));
    }

    public SagaMapper<TData> Correlate<TMessage>(
        Expression<Func<TMessage, object?>> messageProperty)
        where TMessage : IMessage
    {
        return Correlate(CompilePropertyAccessor(messageProperty));
    }

    public SagaMapper<TData> FindWith<TMessage>(
        ISagaFinder<TMessage, TData> finder,
        bool startsSaga = false)
        where TMessage : IMessage
    {
        ArgumentNullException.ThrowIfNull(finder);

        return AddRule<TMessage>(
            startsSaga,
            (message, context, cancellationToken) => finder.FindCorrelationId(message, context, cancellationToken));
    }

    public bool TryGetRule(Type messageType, out SagaCorrelationRule rule)
    {
        ArgumentNullException.ThrowIfNull(messageType);
        return _rules.TryGetValue(messageType, out rule!);
    }

    private SagaMapper<TData> AddRule<TMessage>(
        bool startsSaga,
        Func<TMessage, MiniBusContext, CancellationToken, Task<string?>> resolveCorrelationId)
        where TMessage : IMessage
    {
        var messageType = typeof(TMessage);
        if (_rules.TryGetValue(messageType, out var existingRule))
        {
            throw new SagaMappingException($"Saga data '{typeof(TData).FullName}' already has a correlation mapping for message type '{messageType.FullName}'. Existing StartsSaga={existingRule.StartsSaga}, requested StartsSaga={startsSaga}.");
        }

        _rules.Add(messageType, new SagaCorrelationRule<TMessage>(startsSaga, resolveCorrelationId));
        return this;
    }

    private static Func<TMessage, string?> CompilePropertyAccessor<TMessage>(
        Expression<Func<TMessage, object?>> expression)
        where TMessage : IMessage
    {
        ArgumentNullException.ThrowIfNull(expression);

        var accessor = expression.Compile();
        return message => accessor(message)?.ToString();
    }
}
