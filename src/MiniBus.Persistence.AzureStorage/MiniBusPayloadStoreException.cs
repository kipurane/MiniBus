namespace MiniBus.Persistence.AzureStorage;

public class MiniBusPayloadStoreException : Exception
{
    public MiniBusPayloadStoreException(string message)
        : base(message)
    {
    }

    public MiniBusPayloadStoreException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
