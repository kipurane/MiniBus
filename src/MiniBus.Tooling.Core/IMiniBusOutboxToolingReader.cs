namespace MiniBus.Tooling.Core;

public interface IMiniBusOutboxToolingReader
{
    Task<MiniBusToolingQueryResult<MiniBusOutboxRecord>> ListAsync(
        MiniBusToolingQueryFilter filter,
        CancellationToken cancellationToken = default);
}
