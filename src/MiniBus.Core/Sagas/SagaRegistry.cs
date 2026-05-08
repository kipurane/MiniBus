namespace MiniBus.Core.Sagas;

public sealed class SagaRegistry
{
    private readonly List<SagaDefinition> _definitions = new();
    private readonly Dictionary<Type, List<SagaDefinition>> _definitionsByMessageType = new();

    public IReadOnlyList<SagaDefinition> Definitions => _definitions;

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
        _definitions.Add(definition);

        foreach (var messageType in rules.Keys)
        {
            if (!_definitionsByMessageType.TryGetValue(messageType, out var definitions))
            {
                definitions = new List<SagaDefinition>();
                _definitionsByMessageType.Add(messageType, definitions);
            }

            definitions.Add(definition);
        }

        return definition;
    }

    public IReadOnlyList<SagaDefinition> GetDefinitionsForMessage(Type messageType)
    {
        ArgumentNullException.ThrowIfNull(messageType);

        if (_definitionsByMessageType.TryGetValue(messageType, out var exactMatches))
        {
            return exactMatches;
        }

        return _definitionsByMessageType
            .Where(entry => entry.Key.IsAssignableFrom(messageType))
            .SelectMany(entry => entry.Value)
            .Distinct()
            .ToArray();
    }
}
