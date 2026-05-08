namespace MiniBus.Core.Sagas;

public sealed class SagaDefinition
{
    internal SagaDefinition(
        Type sagaType,
        Type dataType,
        IReadOnlyDictionary<Type, SagaCorrelationRule> rules)
    {
        SagaType = sagaType;
        DataType = dataType;
        Rules = rules;
    }

    public Type SagaType { get; }

    public Type DataType { get; }

    public IReadOnlyDictionary<Type, SagaCorrelationRule> Rules { get; }
}
