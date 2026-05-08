using MiniBus.Core.Context;
using MiniBus.Core.Contracts;

namespace MiniBus.Core.Sagas;

public sealed class SagaInvoker
{
    private static readonly System.Reflection.MethodInfo InvokeDefinitionCoreMethod =
        typeof(SagaInvoker)
            .GetMethod(nameof(InvokeDefinitionCoreAsync), System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Saga invoker core method could not be found.");

    private readonly Dictionary<(Type SagaType, Type DataType, Type MessageType), Func<SagaInvoker, SagaCorrelationRule, object, MiniBusContext, IServiceProvider, CancellationToken, Task>> _invokeDelegates = new();
    private readonly SagaRegistry _registry;
    private readonly ISagaPersistence _persistence;

    public SagaInvoker(SagaRegistry registry, ISagaPersistence persistence)
    {
        _registry = registry;
        _persistence = persistence;
    }

    public async Task InvokeAsync(
        object message,
        MiniBusContext context,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(serviceProvider);

        var messageType = message.GetType();
        foreach (var definition in _registry.GetDefinitionsForMessage(messageType))
        {
            await InvokeDefinitionAsync(
                    definition,
                    message,
                    context,
                    serviceProvider,
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private Task InvokeDefinitionAsync(
        SagaDefinition definition,
        object message,
        MiniBusContext context,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        var rule = ResolveRule(definition, message.GetType());
        var invoke = GetInvokeDelegate(definition.SagaType, definition.DataType, rule.MessageType);
        return invoke(this, rule, message, context, serviceProvider, cancellationToken);
    }

    private Func<SagaInvoker, SagaCorrelationRule, object, MiniBusContext, IServiceProvider, CancellationToken, Task> GetInvokeDelegate(
        Type sagaType,
        Type dataType,
        Type messageType)
    {
        var key = (sagaType, dataType, messageType);
        if (_invokeDelegates.TryGetValue(key, out var invoke))
        {
            return invoke;
        }

        var genericMethod = InvokeDefinitionCoreMethod.MakeGenericMethod(sagaType, dataType, messageType);
        invoke = (invoker, rule, message, context, serviceProvider, cancellationToken) =>
        {
            var result = genericMethod.Invoke(invoker, new object[] { rule, message, context, serviceProvider, cancellationToken });
            return result is Task task
                ? task
                : throw new InvalidOperationException("Saga invoker core method did not return a Task.");
        };
        _invokeDelegates.Add(key, invoke);

        return invoke;
    }

    private async Task InvokeDefinitionCoreAsync<TSaga, TData, TMessage>(
        SagaCorrelationRule rule,
        object message,
        MiniBusContext context,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
        where TSaga : MiniBusSaga<TData>, IHandleSagaMessages<TMessage>, new()
        where TData : class, ISagaData, new()
        where TMessage : IMessage
    {
        var typedMessage = (TMessage)message;
        var correlationId = await rule
            .ResolveCorrelationIdAsync(message, context, cancellationToken)
            .ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(correlationId))
        {
            throw new SagaMappingException($"Saga '{typeof(TSaga).FullName}' resolved an empty correlation id for message type '{typeof(TMessage).FullName}'.");
        }

        var loaded = await _persistence
            .LoadAsync<TData>(correlationId, cancellationToken)
            .ConfigureAwait(false);

        if (loaded?.Data.IsCompleted == true)
        {
            return;
        }

        var isNew = loaded is null;
        if (isNew && !rule.StartsSaga)
        {
            return;
        }

        var data = loaded?.Data ?? new TData
        {
            Id = Guid.NewGuid(),
            CorrelationId = correlationId
        };

        var saga = serviceProvider.GetService(typeof(TSaga)) as TSaga ?? new TSaga();
        saga.AttachData(data);

        await saga.Handle(typedMessage, context, cancellationToken).ConfigureAwait(false);

        if (isNew)
        {
            await _persistence.CreateAsync(data, cancellationToken).ConfigureAwait(false);
        }

        if (data.IsCompleted)
        {
            await _persistence.CompleteAsync(data, loaded?.Version, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (!isNew)
        {
            await _persistence.SaveAsync(data, loaded?.Version, cancellationToken).ConfigureAwait(false);
        }
    }

    private static SagaCorrelationRule ResolveRule(SagaDefinition definition, Type messageType)
    {
        var matches = definition.Rules
            .Where(rule => rule.Key.IsAssignableFrom(messageType))
            .Select(rule => rule.Value)
            .ToArray();

        return matches.Length switch
        {
            1 => matches[0],
            0 => throw new SagaMappingException($"Saga '{definition.SagaType.FullName}' has no correlation mapping for message type '{messageType.FullName}'."),
            _ => throw new SagaMappingException($"Saga '{definition.SagaType.FullName}' has multiple correlation mappings that match message type '{messageType.FullName}'.")
        };
    }
}
