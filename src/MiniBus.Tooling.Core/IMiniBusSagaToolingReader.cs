namespace MiniBus.Tooling.Core;

public interface IMiniBusSagaToolingReader
{
    Task<MiniBusToolingQueryResult<MiniBusSagaRecord>> ListAsync(
        MiniBusToolingQueryFilter filter,
        CancellationToken cancellationToken = default);
}
