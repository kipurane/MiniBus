namespace MiniBus.Persistence.AzureStorage;

public interface IMiniBusPayloadStore
{
    Task<MiniBusPayloadReference> WriteAsync(
        BinaryData payload,
        MiniBusPayloadWriteOptions? options = null,
        CancellationToken cancellationToken = default);

    Task<MiniBusPayloadReference> WriteAsync(
        Stream payload,
        MiniBusPayloadWriteOptions? options = null,
        CancellationToken cancellationToken = default);

    Task<Stream> OpenReadAsync(
        MiniBusPayloadReference reference,
        CancellationToken cancellationToken = default);

    Task<BinaryData> ReadAsync(
        MiniBusPayloadReference reference,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        MiniBusPayloadReference reference,
        CancellationToken cancellationToken = default);
}
