namespace MiniBus.Core.Auditing;

public interface IMiniBusAuditWriter
{
    Task WriteAsync(
        MiniBusAuditRecord record,
        CancellationToken cancellationToken = default);
}
