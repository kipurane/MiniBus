namespace MiniBus.Core.Persistence;

public interface IMiniBusPersistenceSessionFactory
{
    ValueTask<IMiniBusPersistenceSession> CreateAsync(
        CancellationToken cancellationToken = default);
}
