namespace MiniBus.Tooling.Core;

public interface IMiniBusOutboxDrainAction
{
    Task<MiniBusOutboxDrainResult> DrainAsync(
        MiniBusOutboxDrainRequest request,
        CancellationToken cancellationToken = default);
}
