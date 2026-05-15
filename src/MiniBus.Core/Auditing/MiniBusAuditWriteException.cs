namespace MiniBus.Core.Auditing;

public sealed class MiniBusAuditWriteException : Exception
{
    public MiniBusAuditWriteException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
