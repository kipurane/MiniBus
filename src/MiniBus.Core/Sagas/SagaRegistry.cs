using System.Collections.Immutable;

namespace MiniBus.Core.Sagas;

public sealed class SagaRegistry
{
    private readonly object _syncRoot = new();
    private SagaRegistryState _state = SagaRegistryState.Empty;

    public IReadOnlyList<SagaDefinition> Definitions => Volatile.Read(ref _state).Definitions;

    public SagaDefinition Register<TSaga, TData>()
        where TSaga : MiniBusSaga<TData>, new()
        where TData : class, ISagaData, new()
    {
        var saga = new TSaga();
        var mapper = new SagaMapper<TData>();
        saga.ConfigureHowToFindSaga(mapper);

        var rules = mapper.Rules.ToDictionary(rule => rule.MessageType, rule => rule);
        if (rules.Count == 0)
        {
            throw new SagaMappingException($"Saga '{typeof(TSaga).FullName}' must configure at least one message correlation mapping.");
        }

        var definition = new SagaDefinition(typeof(TSaga), typeof(TData), rules);

        lock (_syncRoot)
        {
            var currentState = Volatile.Read(ref _state);
            var updatedDefinitions = currentState.Definitions.Add(definition);

            var updatedDefinitionsByMessageType = new Dictionary<Type, ImmutableArray<SagaDefinition>>(currentState.DefinitionsByMessageType);
            foreach (var messageType in rules.Keys)
            {
                if (currentState.DefinitionsByMessageType.TryGetValue(messageType, out var existingDefinitions))
                {
                    updatedDefinitionsByMessageType[messageType] = existingDefinitions.Add(definition);
                    continue;
                }

                updatedDefinitionsByMessageType[messageType] = ImmutableArray.Create(definition);
            }

            Volatile.Write(ref _state, new SagaRegistryState(updatedDefinitions, updatedDefinitionsByMessageType));
        }

        return definition;
    }

    public IReadOnlyList<SagaDefinition> GetDefinitionsForMessage(Type messageType)
    {
        ArgumentNullException.ThrowIfNull(messageType);

        var state = Volatile.Read(ref _state);
        if (state.DefinitionsByMessageType.TryGetValue(messageType, out var exactMatches))
        {
            return exactMatches;
        }

        return state.DefinitionsByMessageType
            .Where(entry => entry.Key.IsAssignableFrom(messageType))
            .SelectMany(entry => entry.Value)
            .Distinct()
            .ToImmutableArray();
    }

    private sealed class SagaRegistryState
    {
        public static SagaRegistryState Empty { get; } = new(
            ImmutableArray<SagaDefinition>.Empty,
            new Dictionary<Type, ImmutableArray<SagaDefinition>>());

        public SagaRegistryState(
            ImmutableArray<SagaDefinition> definitions,
            Dictionary<Type, ImmutableArray<SagaDefinition>> definitionsByMessageType)
        {
            Definitions = definitions;
            DefinitionsByMessageType = definitionsByMessageType;
        }

        public ImmutableArray<SagaDefinition> Definitions { get; }

        public Dictionary<Type, ImmutableArray<SagaDefinition>> DefinitionsByMessageType { get; }
    }
}
