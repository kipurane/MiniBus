namespace MiniBus.Core.Persistence;

public sealed class MiniBusPersistenceCommitException : Exception
{
    public MiniBusPersistenceCommitException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
