namespace MiniBus.Core.ClaimCheck;

public interface IMiniBusClaimCheckPayloadStore
{
    Task<MiniBusClaimCheckPayloadReference> WriteAsync(
        BinaryData payload,
        MiniBusClaimCheckPayloadWriteOptions? options = null,
        CancellationToken cancellationToken = default);

    Task<BinaryData> ReadAsync(
        MiniBusClaimCheckPayloadReference reference,
        CancellationToken cancellationToken = default);
}
