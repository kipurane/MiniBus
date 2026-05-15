namespace MiniBus.Persistence.AzureStorage;

public sealed class MiniBusPayloadNotFoundException : MiniBusPayloadStoreException
{
    public MiniBusPayloadNotFoundException(MiniBusPayloadReference reference)
        : base($"MiniBus payload blob '{reference.BlobName}' was not found in container '{reference.ContainerName}'.")
    {
        Reference = reference;
    }

    public MiniBusPayloadReference Reference { get; }
}
