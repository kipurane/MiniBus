namespace MiniBus.Core.ClaimCheck;

public sealed class MiniBusClaimCheckPayloadNotFoundException : MiniBusClaimCheckException
{
    public MiniBusClaimCheckPayloadNotFoundException(MiniBusClaimCheckPayloadReference reference, Exception? innerException = null)
        : base($"MiniBus claim-check payload '{reference.BlobName}' was not found in container '{reference.ContainerName}'.", innerException)
    {
        Reference = reference;
    }

    public MiniBusClaimCheckPayloadReference Reference { get; }
}
