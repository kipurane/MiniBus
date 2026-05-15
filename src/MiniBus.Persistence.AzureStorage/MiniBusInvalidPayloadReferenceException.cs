namespace MiniBus.Persistence.AzureStorage;

public sealed class MiniBusInvalidPayloadReferenceException : MiniBusPayloadStoreException
{
    public MiniBusInvalidPayloadReferenceException(string message)
        : base(message)
    {
    }
}
