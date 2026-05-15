namespace MiniBus.Core.Auditing;

public sealed class DisabledMiniBusAuditWriter : IMiniBusAuditWriter
{
    public static DisabledMiniBusAuditWriter Instance { get; } = new();

    private DisabledMiniBusAuditWriter()
    {
    }

    public Task WriteAsync(
        MiniBusAuditRecord record,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
