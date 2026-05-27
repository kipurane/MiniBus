namespace MiniBus.Tooling.Core;

public interface IMiniBusInboxToolingReader
{
    Task<MiniBusToolingQueryResult<MiniBusInboxRecord>> ListAsync(
        MiniBusToolingQueryFilter filter,
        CancellationToken cancellationToken = default);
}
