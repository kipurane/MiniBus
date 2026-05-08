namespace MiniBus.Core.Sagas;

public sealed class SagaPersistenceException : InvalidOperationException
{
    public SagaPersistenceException(string message)
        : base(message)
    {
    }
}
