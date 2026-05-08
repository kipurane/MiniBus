namespace MiniBus.Core.Sagas;

public sealed class SagaMappingException : InvalidOperationException
{
    public SagaMappingException(string message)
        : base(message)
    {
    }
}
