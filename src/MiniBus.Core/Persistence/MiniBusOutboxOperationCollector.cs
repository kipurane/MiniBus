namespace MiniBus.Core.Persistence;

public sealed class MiniBusOutboxOperationCollector
{
    private readonly List<MiniBusOutboxOperation> _operations = new();

    public IReadOnlyList<MiniBusOutboxOperation> Operations => _operations;

    public void Add(MiniBusOutboxOperation operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        _operations.Add(operation);
    }
}
